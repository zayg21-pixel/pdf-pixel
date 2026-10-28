const views = new Map();
let interop = null;

function isPlainObject(value) {
    return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function mergeConfiguration(defaults, overrides) {
    const merged = { ...defaults };

    for (const [key, value] of Object.entries(overrides || {})) {
        merged[key] = isPlainObject(value) && isPlainObject(defaults[key])
            ? mergeConfiguration(defaults[key], value)
            : value;
    }

    return merged;
}

/**
 * Creates a canvas placed at the top-left corner of the container that lets pointer events through.
 * @param {string} className Class name of the canvas.
 * @returns {HTMLCanvasElement}
 */
function createPanelCanvas(className) {
    const canvas = document.createElement('canvas');
    canvas.className = className;
    canvas.style.display = 'block';
    canvas.style.position = 'absolute';
    canvas.style.top = '0';
    canvas.style.left = '0';
    canvas.style.pointerEvents = 'none';
    canvas.style.zIndex = '1';
    return canvas;
}

/**
 * Visible page of a presented frame. Mirrors `PdfPanelFramePage`.
 */
class PdfPanelFramePage {
    /**
     * @param {{ pageNumber: number, label: string, width: number, height: number,
     * rotation: number, rotatedWidth: number, rotatedHeight: number, panelToContent: number[] }} page
     * The page of the frame JSON.
     */
    constructor(page) {
        this.pageNumber = page.pageNumber;
        this.info = {
            label: page.label,
            width: page.width,
            height: page.height,
            rotation: page.rotation
        };
        this.rotatedSize = { width: page.rotatedWidth, height: page.rotatedHeight };
        this._panelToContent = new DOMMatrix(page.panelToContent);
    }

    /**
     * Returns the matrix that maps panel pixels to page space rotated by `rotation` degrees,
     * with the origin at the top-left corner of the rotated page.
     * @param {number} rotation Rotation of the page space relative to the unrotated page content, a multiple of 90.
     * @returns {DOMMatrix}
     */
    getPanelToPage(rotation) {
        const normalizedRotation = ((rotation % 360) + 360) % 360;

        if (normalizedRotation % 90 !== 0) {
            throw new RangeError(`Rotation ${rotation} is not a multiple of 90`);
        }

        const width = this.info.width;
        const height = this.info.height;
        const rotatedWidth = (normalizedRotation % 180 === 0) ? width : height;
        const rotatedHeight = (normalizedRotation % 180 === 0) ? height : width;
        const rotationOffsetX = (normalizedRotation === 90 || normalizedRotation === 180) ? rotatedWidth : 0;
        const rotationOffsetY = (normalizedRotation === 180 || normalizedRotation === 270) ? rotatedHeight : 0;

        const contentToPage = new DOMMatrix()
            .translate(rotationOffsetX, rotationOffsetY)
            .rotate(normalizedRotation);

        return contentToPage.multiply(this._panelToContent);
    }
}

class PdfPanelView {
    constructor(id, containerElement, configuration) {
        this.id = id;
        this.container = containerElement;
        if (window.getComputedStyle(containerElement).position === 'static') {
            containerElement.style.position = 'relative';
        }

        // The panel canvas and the overlay canvas are sized to the scroll host's client area and never move.
        // The transparent scroll host above them provides native scrollbars and receives pointer and scroll events.
        this.canvas = createPanelCanvas('pdf-panel-canvas');    
        this.overlayCanvas = createPanelCanvas('pdf-panel-overlay-canvas');

        this.scrollHost = document.createElement('div');
        this.scrollHost.className = 'pdf-panel-scroll-host';
        this.scrollHost.style.position = 'absolute';
        this.scrollHost.style.inset = '0';
        this.scrollHost.style.overflow = 'auto';
        this.scrollHost.style.zIndex = '2';

        this.spacer = document.createElement('div');
        this.spacer.className = 'pdf-panel-scroll-spacer';
        this.scrollHost.append(this.spacer);

        containerElement.prepend(this.canvas, this.overlayCanvas, this.scrollHost);

        const defaults = {
            useWebGL: true,
            scrollStep: 20,
            yieldInterval: 16
        };
        this.configuration = mergeConfiguration(defaults, configuration);

        this.state = {
            verticalOffset: 0,
            horizontalOffset: 0,
            scale: 1.0,
            scrollWidth: 0,
            scrollHeight: 0,
            panelWidth: 0,
            panelHeight: 0,
            containerWidth: 0,
            containerHeight: 0,
            devicePixelScale: 1,
            mouseX: null,
            mouseY: null,
            currentPage: 0,
            forcePageSet: 0,
            pointerPressed: false,
            annotationPopup: null,
            activeContentName: null,
            autoScaleMode: 'noAutoScale',
            cursorStyle: null,
            searchQuery: null,
            currentSearchResult: null,
            searchResults: [],
            isTextExtracted: false,
            layerConfiguration: null,
            visibleLayers: [],
            zoomRequest: null
        };

        this._renderScheduled = false;

        // Tracks the scroll position we set programmatically so onScroll can
        // ignore those events and only react to genuine user-initiated scrolls.
        this._expectedScrollLeft = 0;
        this._expectedScrollTop = 0;
        this.onStateChanged = null;
        this.onFramePresented = null;
        this.onThumbnailUpdated = null;
        this.onActionRequested = null;
        this.onCredentialRequested = null;

        // Tracks the initial pinch distance and scale for two-finger zoom gestures.
        this._touchStartDistance = 0;
        this._touchStartScale = null;

        this.onWheel = this.onWheel.bind(this);
        this.onScroll = this.onScroll.bind(this);
        this.onMouseMove = this.onMouseMove.bind(this);
        this.onResizeRequested = this.onResizeRequested.bind(this);
        this.onPointerDown = this.onPointerDown.bind(this);
        this.onPointerUp = this.onPointerUp.bind(this);
        this.onTouchStart = this.onTouchStart.bind(this);
        this.onTouchMove = this.onTouchMove.bind(this);
        this.onTouchEnd = this.onTouchEnd.bind(this);
        this.onKeyDown = this.onKeyDown.bind(this);
    }

    requestRender() {
        try {
            this.performRender();
        }
        catch (err) {
            console.error(`Error during render of view '${this.id}': ${err?.message || String(err)}`);
        }
    }

    /**
     * Requests a render on the next animation frame. Several requests before that frame result in one render.
     */
    scheduleRender() {
        if (this._renderScheduled) {
            return;
        }

        this._renderScheduled = true;
        requestAnimationFrame(() => {
            this._renderScheduled = false;
            this.requestRender();
        });
    }

    performRender() {
        const containerWidth = this.scrollHost.clientWidth;
        const containerHeight = this.scrollHost.clientHeight;

        const dpr = window.devicePixelRatio || 1;
        const zoom = (window.visualViewport && window.visualViewport.scale) ? window.visualViewport.scale : 1;
        const devicePixelScale = dpr * zoom;

        const physicalWidth = Math.round(containerWidth * devicePixelScale);
        const physicalHeight = Math.round(containerHeight * devicePixelScale);

        const pointerInside = this.state.mouseX !== null && this.state.mouseY !== null;
        const pointerX = pointerInside ? this.state.mouseX * devicePixelScale : 0;
        const pointerY = pointerInside ? this.state.mouseY * devicePixelScale : 0;

        const input = {
            panelWidth: physicalWidth,
            panelHeight: physicalHeight,
            devicePixelScale: devicePixelScale,
            verticalOffset: this.state.verticalOffset,
            horizontalOffset: this.state.horizontalOffset,
            scale: this.state.scale,
            autoScaleMode: this.state.autoScaleMode,
            zoomRequest: this.state.zoomRequest,
            forcePageSet: this.state.forcePageSet,
            pointerInside: pointerInside,
            pointerX: pointerX,
            pointerY: pointerY,
            pointerPressed: this.state.pointerPressed,
            searchQuery: this.state.searchQuery,
            currentSearchResult: this.state.currentSearchResult
        };

        const output = interop.RequestRedraw(this.id, JSON.stringify(input));

        // Without a document the panel returns no state; the view keeps what it sent.
        const redrawState = output
            ? JSON.parse(output)
            : {
                scrollWidth: 0,
                scrollHeight: 0,
                verticalOffset: input.verticalOffset,
                horizontalOffset: input.horizontalOffset,
                scale: input.scale,
                autoScaleMode: input.autoScaleMode
            };

        this.state.forcePageSet = 0;
        this.state.zoomRequest = null;
        this.state.containerWidth = containerWidth;
        this.state.containerHeight = containerHeight;
        this.state.devicePixelScale = devicePixelScale;
        this.state.panelWidth = input.panelWidth;
        this.state.panelHeight = input.panelHeight;
        this.state.scrollWidth = redrawState.scrollWidth;
        this.state.scrollHeight = redrawState.scrollHeight;
        this.state.verticalOffset = redrawState.verticalOffset;
        this.state.horizontalOffset = redrawState.horizontalOffset;
        this.state.scale = redrawState.scale;

        this.state.autoScaleMode = redrawState.autoScaleMode;
        this.state.currentPage = redrawState.currentPage;
        this.state.isTextExtracted = redrawState.isTextExtracted;

        if (redrawState.searchResultsChanged) {
            this.state.searchResults = redrawState.searchResults || [];
        }

        if (redrawState.layersChanged) {
            this.state.layerConfiguration = redrawState.layerConfiguration ?? null;
            this.state.visibleLayers = redrawState.visibleLayers || [];
        }

        if (redrawState.annotationPopupChanged) {
            this.state.annotationPopup = redrawState.annotationPopup || null;
        }

        if (redrawState.activeContentNameChanged) {
            this.state.activeContentName = redrawState.activeContentName || null;
        }

        this.state.cursorStyle = redrawState.cursorStyle || null;
        this.scrollHost.style.cursor = this.state.cursorStyle || '';

        this.spacer.style.width = (this.state.scrollWidth / dpr) + 'px';
        this.spacer.style.height = (this.state.scrollHeight / dpr) + 'px';

        // Force a synchronous layout so the new spacer dimensions are applied to
        // the scroll bounds before we set the position. Without this, scrollLeft/
        // scrollTop would be validated against the old bounds and may be clamped.
        void this.scrollHost.offsetHeight;

        this.scrollHost.style.overflow = 'auto';
        this.scrollHost.scrollLeft = this.state.horizontalOffset / dpr;
        this.scrollHost.scrollTop = this.state.verticalOffset / dpr;

        // Read back the browser-clamped values so onScroll can recognise
        // and suppress the scroll event that this programmatic set fires.
        this._expectedScrollLeft = this.scrollHost.scrollLeft;
        this._expectedScrollTop = this.scrollHost.scrollTop;

        void this.scrollHost.offsetHeight;

        if (typeof this.onStateChanged === 'function') {
            this.onStateChanged({ ...this.state });
        }

        if (redrawState.actionRequests && typeof this.onActionRequested === 'function') {
            for (const request of redrawState.actionRequests) {
                this.onActionRequested(request);
            }
        }
    }

    onWheel(e) {
        e.preventDefault();

        if (e.ctrlKey) {
            // Zoom around the mouse if available; otherwise around the center
            let centerX = this.state.panelWidth / 2;
            let centerY = this.state.panelHeight / 2;

            if (this.state.mouseX !== null && this.state.mouseY !== null) {
                centerX = this.state.mouseX * this.state.devicePixelScale;
                centerY = this.state.mouseY * this.state.devicePixelScale;
            }

            this.state.zoomRequest = { step: e.deltaY > 0 ? -1 : 1, centerX: centerX, centerY: centerY };
        } else {
            let deltaX = e.deltaX;
            let deltaY = e.deltaY;

            // Convert line/page deltas to pixel deltas
            if (e.deltaMode === WheelEvent.DOM_DELTA_LINE) {
                deltaX *= this.configuration.scrollStep;
                deltaY *= this.configuration.scrollStep;
            } else if (e.deltaMode === WheelEvent.DOM_DELTA_PAGE) {
                deltaX *= this.scrollHost.clientWidth;
                deltaY *= this.scrollHost.clientHeight;
            }

            const dpr = window.devicePixelRatio || 1;
            this.state.verticalOffset += deltaY * dpr;
            this.state.horizontalOffset += deltaX * dpr;
        }

        this.requestRender();
    }

    onScroll() {
        // Suppress scroll events that were fired by our own programmatic
        // scrollLeft/scrollTop assignments inside performRender().
        if (this.scrollHost.scrollLeft === this._expectedScrollLeft &&
            this.scrollHost.scrollTop === this._expectedScrollTop) {
            return;
        }

        const dpr = window.devicePixelRatio || 1;
        this.state.verticalOffset = this.scrollHost.scrollTop * dpr;
        this.state.horizontalOffset = this.scrollHost.scrollLeft * dpr;
        this.requestRender();
    }

    onMouseMove(e) {
        // Track mouse position relative to the scroll host for zoom-centering.
        const rect = this.scrollHost.getBoundingClientRect();
        const insideScrollHost =
            e.clientX >= rect.left &&
            e.clientX <= rect.right &&
            e.clientY >= rect.top &&
            e.clientY <= rect.bottom;

        if (insideScrollHost) {
            this.state.mouseX = e.clientX - rect.left;
            this.state.mouseY = e.clientY - rect.top;
        } else {
            this.state.mouseX = null;
            this.state.mouseY = null;
        }

        this.requestRender();
    }

    onPointerDown() {
        this.state.pointerPressed = true;
        this.requestRender();
    }

    onPointerUp() {
        this.state.pointerPressed = false;
        this.requestRender();
    }

    _getTouchDistance(touches) {
        const dx = touches[0].clientX - touches[1].clientX;
        const dy = touches[0].clientY - touches[1].clientY;
        return Math.sqrt(dx * dx + dy * dy);
    }

    _getTouchMidpoint(touches) {
        return {
            x: (touches[0].clientX + touches[1].clientX) / 2,
            y: (touches[0].clientY + touches[1].clientY) / 2
        };
    }

    onTouchStart(e) {
        if (e.touches.length === 2) {
            e.preventDefault();
            this._touchStartDistance = this._getTouchDistance(e.touches);
            this._touchStartScale = this.state.scale;
        }
    }

    onTouchMove(e) {
        if (e.touches.length !== 2 || this._touchStartDistance === 0) {
            return;
        }

        e.preventDefault();

        const currentDistance = this._getTouchDistance(e.touches);
        const scaleFactor = currentDistance / this._touchStartDistance;
        const midpoint = this._getTouchMidpoint(e.touches);
        const rect = this.scrollHost.getBoundingClientRect();
        const centerX = (midpoint.x - rect.left) * this.state.devicePixelScale;
        const centerY = (midpoint.y - rect.top) * this.state.devicePixelScale;

        this.state.zoomRequest = { scale: this._touchStartScale * scaleFactor, centerX: centerX, centerY: centerY };

        this.requestRender();
    }

    onTouchEnd(e) {
        if (e.touches.length < 2) {
            this._touchStartDistance = 0;
            this._touchStartScale = null;
        }
    }

    onKeyDown(e) {
        if (!(e.ctrlKey || e.metaKey) || e.key !== 'c') {
            return;
        }

        const content = interop.GetSelectedContent(this.id);
        if (!content) {
            return;
        }

        e.preventDefault();

        const written = (content.type === 'image')
            ? navigator.clipboard.write([new ClipboardItem({ 'image/png': new Blob([content.png], { type: 'image/png' }) })])
            : navigator.clipboard.writeText(content.text);

        written.catch(err => console.warn(`Failed to write clipboard for view '${this.id}':`, err));
    }

    onResizeRequested() {
        const hasHorizontalScrollbar = this.state.scrollWidth > this.state.panelWidth;
        const hasVerticalScrollbar = this.state.scrollHeight > this.state.panelHeight;

        this.scrollHost.style.overflowX = hasHorizontalScrollbar ? 'scroll' : 'hidden';
        this.scrollHost.style.overflowY = hasVerticalScrollbar ? 'scroll' : 'hidden';

        this.requestRender();
    }

    attachEvents() {
        this.container.addEventListener('wheel', this.onWheel, { passive: false });
        this.scrollHost.addEventListener('scroll', this.onScroll);
        this.scrollHost.addEventListener('pointerdown', this.onPointerDown);
        this.scrollHost.addEventListener('touchstart', this.onTouchStart, { passive: false });
        this.scrollHost.addEventListener('touchmove', this.onTouchMove, { passive: false });
        this.scrollHost.addEventListener('touchend', this.onTouchEnd);
        document.addEventListener('mousemove', this.onMouseMove);
        document.addEventListener('pointerup', this.onPointerUp);
        document.addEventListener('keydown', this.onKeyDown);

        this.resizeObserver = new ResizeObserver(this.onResizeRequested);
        this.resizeObserver.observe(this.container);
    }

    detachEvents() {
        this.container.removeEventListener('wheel', this.onWheel);
        this.scrollHost.removeEventListener('scroll', this.onScroll);
        this.scrollHost.removeEventListener('pointerdown', this.onPointerDown);
        this.scrollHost.removeEventListener('touchstart', this.onTouchStart);
        this.scrollHost.removeEventListener('touchmove', this.onTouchMove);
        this.scrollHost.removeEventListener('touchend', this.onTouchEnd);
        document.removeEventListener('mousemove', this.onMouseMove);
        document.removeEventListener('pointerup', this.onPointerUp);
        document.removeEventListener('keydown', this.onKeyDown);

        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            this.resizeObserver = null;
        }
    }

    initInterop() {
        void interop.RegisterPanel(this.id, this.configuration);
    }

    /**
     * Updates the configuration keys that are set in `configuration`; keys that are not set keep their current values.
     * `useWebGL` and `yieldInterval` apply only to the initial configuration.
     * @param {object} configuration `scrollStep` and the properties of `PdfPanelConfiguration` in camelCase.
     */
    updateConfiguration(configuration) {
        if (configuration.scrollStep !== undefined) {
            this.configuration.scrollStep = configuration.scrollStep;
        }

        this.applyBackgroundColor(configuration);
        interop.UpdateConfiguration(this.id, configuration);
        this.requestRender();
    }

    /**
     * Paints the container with `panelBackgroundColor` when the configuration sets it.
     * @param {object} configuration The configuration.
     */
    applyBackgroundColor(configuration) {
        const backgroundColor = configuration.panelBackgroundColor;

        if (backgroundColor) {
            this.container.style.backgroundColor = backgroundColor;
        }
    }

    /**
     * Selects the search result after (`step` 1) or before (`step` -1) the current one, wrapping around,
     * or the first result on or after the current page when none is current.
     * @param {number} step 1 for the next result, -1 for the previous one.
     */
    selectSearchResult(step) {
        const results = this.state.searchResults;

        if (results.length === 0) {
            return;
        }

        const current = this.state.currentSearchResult;
        const currentIndex = current
            ? results.findIndex(result => result.pageNumber === current.pageNumber
                && result.startIndex === current.startIndex
                && result.length === current.length)
            : -1;

        let index;

        if (currentIndex >= 0) {
            index = (currentIndex + step + results.length) % results.length;
        } else {
            index = Math.max(results.findIndex(result => result.pageNumber >= this.state.currentPage), 0);
        }

        const result = results[index];
        this.state.currentSearchResult = { pageNumber: result.pageNumber, startIndex: result.startIndex, length: result.length };
        this.requestRender();
    }

    start() {
        this.applyBackgroundColor(this.configuration);
        this.attachEvents();
        this.initInterop();
        this.requestRender();
        console.log(`View '${this.id}' registered successfully`);
    }

    /**
     * Sizes the overlay canvas to the presented frame and passes it to the subscriber with the view id and the frame.
     * @param {object} frame The frame object.
     */
    presentFrame(frame) {
        const { width, height } = frame.panelSize;

        if (this.overlayCanvas.width !== width || this.overlayCanvas.height !== height) {
            this.overlayCanvas.width = width;
            this.overlayCanvas.height = height;
        }

        this.overlayCanvas.style.width = this.canvas.style.width;
        this.overlayCanvas.style.height = this.canvas.style.height;

        if (typeof this.onFramePresented === 'function') {
            this.onFramePresented({ id: this.id, canvas: this.overlayCanvas, frame: frame });
        }
    }

    /**
     * Resets the view state of the previous document: the scroll position, the current page and the state reported
     * for its content.
     */
    resetDocumentState() {
        this.state.verticalOffset = 0;
        this.state.horizontalOffset = 0;
        this.state.currentPage = 0;
        this.state.forcePageSet = 0;
        this.state.zoomRequest = null;
        this.state.currentSearchResult = null;
        this.state.searchResults = [];
        this.state.annotationPopup = null;
        this.state.activeContentName = null;
        this.state.layerConfiguration = null;
        this.state.visibleLayers = [];
    }

    /**
     * Passes a thumbnail update to the subscriber of the view.
     * @param {{ pageNumber: number, png: Uint8Array }} update The thumbnail update.
     */
    updateThumbnail(update) {
        if (typeof this.onThumbnailUpdated === 'function') {
            this.onThumbnailUpdated(update);
        }
    }

    dispose() {
        this.detachEvents();
        this.canvas.remove();
        this.overlayCanvas.remove();
        this.scrollHost.remove();
    }
}

/**
 * Initialize PDF panel interop and bind JS module imports.
 * @param {(name: string, module: any) => void} setModuleImports Binds a logical module name to an ESM object for [JSImport].
 * @param {(assemblyName: string) => Promise<any>} getAssemblyExports Retrieves .NET assembly exports.
 * @returns {Promise<void>} Resolves when interop is ready.
 */
export async function initialize(setModuleImports, getAssemblyExports) {
    const exports = await getAssemblyExports(`PdfPixel.PdfPanel.Web`);
    const panelInterop = exports.PdfPixel.PdfPanel.Web.PdfPanelInterop;

    setModuleImports('canvasInterop.js', this);

    interop = panelInterop;
    interop.Initialize();

    console.log('Panel interop initialized');
}

/**
 * Register a PDF panel view bound to a container element.
 * @param {string} id Unique view id.
 * @param {HTMLElement} containerElement The container element with the id `id`; the panel adds its canvases and scroll host to it.
 * @param {object} [configuration] `useWebGL`, `scrollStep`, `yieldInterval` (ms) and the properties of `PdfPanelConfiguration` in camelCase.
 * @returns {Promise<boolean>} True if registration succeeded.
 */
export function registerPanel(id, containerElement, configuration) {
    if (!containerElement) {
        console.error(`Container element is null or undefined for id '${id}'`);
        return false;
    }
    if (views.has(id)) {
        console.warn(`View with id '${id}' is already registered`);
        return false;
    }

    let view;
    try {
        view = new PdfPanelView(id, containerElement, configuration);
    } catch (err) {
        console.error(err?.message || String(err));
        return false;
    }

    views.set(id, view);
    view.start();
    return true;
}

/**
 * Unregister and dispose a PDF panel view.
 * @param {string} id View id to unregister.
 * @returns {Promise<boolean>} True if unregistered.
 */
export function unregisterPanel(id) {
    if (!views.has(id)) {
        console.warn(`View with id '${id}' is not registered`);
        return false;
    }
    const view = views.get(id);
    if (view) {
        view.dispose();
    }
    views.delete(id);
    console.log(`View '${id}' unregistered successfully`);
    interop.UnregisterPanel(id);
    return true;
}

/**
 * Set the PDF document for the specified view. The shown document is kept when loading fails.
 * An encrypted document asks the `setOnCredentialRequested` callback for its credential until one is accepted or the
 * callback returns null.
 * @param {string} id View id.
 * @param {Uint8Array} documentData PDF file bytes.
 * @returns {Promise<object>} The result of loading the document: on success `ok: true` with the document data
 * (`pageCount`, `version`, `pageMode`, `pageLayout`, `language`, `information`, `permissions`, `pages`, `outlines`,
 * `layerConfigurations`, `attachments`), otherwise `{ ok: false, reason, message }` with `reason` one of `empty`,
 * `encrypted`, `unsupported`, `invalid` and `error`.
 */
export async function setDocument(id, documentData) {
    const view = views.get(id);
    const result = await performWithCredential(view, credential => JSON.parse(interop.SetDocument(id, documentData, credential)));

    if (!result.ok) {
        return { ok: false, reason: result.reason, message: result.message };
    }

    view?.resetDocumentState();
    return result;
}

/**
 * Performs `attempt` without a credential, then, while its result requests a credential, again with the credential the
 * `setOnCredentialRequested` callback of `view` returns, until the result requests none or the callback returns null.
 * @param {PdfPanelView | undefined} view The view.
 * @param {(credential: object | null) => object | null} attempt Performs the request with a credential.
 * @returns {Promise<object | null>} The last result.
 */
async function performWithCredential(view, attempt) {
    let result = attempt(null);

    while (result && result.credentialRequest && view && typeof view.onCredentialRequested === 'function') {
        const request = result.credentialRequest;
        const credential = await view.onCredentialRequested({ reason: request.reason, authEvent: request.authEvent });
        if (!credential) {
            break;
        }

        result = attempt(credential);
    }

    return result;
}

/**
 * Subscribe to credential requests of encrypted documents loaded into the specified view. The callback receives
 * `{ reason, authEvent }`, `reason` being `required` for the first request and `rejected` after a credential that is not
 * accepted, and returns the credential, such as `{ type: 'password', password }`, or null to stop, directly or as a
 * promise.
 * @param {string} id View id.
 * @param {(request: { reason: 'required' | 'rejected', authEvent: 'documentOpen' | 'embeddedFileOpen' }) => object | null | Promise<object | null>} callback
 * Called with each credential request.
 * @returns {boolean} True if the view was found and the callback was registered.
 */
export function setOnCredentialRequested(id, callback) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.onCredentialRequested = callback;
    return true;
}

/**
 * Activate an outline item of the specified view: perform its action, or navigate to its destination when it has none.
 * @param {string} id View id.
 * @param {number} handle Handle of an item of `outlines` in the document data.
 * @returns {boolean} True if the view has a document and the item was activated.
 */
export function activateOutlineItem(id, handle) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    const activated = interop.ActivateOutlineItem(id, handle);
    view.requestRender();
    return activated;
}

/**
 * Render the content of the specified view with a layer configuration; the current one is reported as
 * `layerConfiguration` in the view state.
 * @param {string} id View id.
 * @param {number} handle Handle of a configuration of `layerConfigurations` in the document data.
 * @returns {boolean} True if the view has a document and the configuration was set.
 */
export function setLayerConfiguration(id, handle) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    const applied = interop.SetLayerConfiguration(id, handle);
    view.requestRender();
    return applied;
}

/**
 * Show or hide a layer of the specified view; showing it hides the other layers of its radio-button sets.
 * The visible layers are reported as `visibleLayers` in the view state.
 * @param {string} id View id.
 * @param {number} handle Handle of a layer of `layerConfigurations` in the document data.
 * @param {boolean} visible Whether the layer is visible.
 * @returns {boolean} True if the view has a document and the visibility was set.
 */
export function setLayerVisible(id, handle, visible) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    const applied = interop.SetLayerVisible(id, handle, visible);
    view.requestRender();
    return applied;
}

/**
 * Get the content of an embedded file of the specified view. An encrypted file asks the `setOnCredentialRequested`
 * callback for its credential until one is accepted or the callback returns null.
 * @param {string} id View id.
 * @param {number} handle Handle of a file, from `attachments` in the document data or from a requested action.
 * @returns {Promise<Uint8Array | null>} The file content, or null when the view has no document, there is no such file or
 * it is not opened without a credential.
 */
export async function getFileBytes(id, handle) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return null;
    }

    const result = await performWithCredential(view, credential => interop.GetFileBytes(id, handle, credential));
    return (result && result.ok) ? result.bytes : null;
}

/**
 * Subscribe to the actions the specified view does not perform itself. The callback receives `{ source, action }`
 * for each requested action, after the render during which it was requested.
 * @param {string} id View id.
 * @param {(request: { source: string, action: object }) => void} callback Called with each requested action.
 * @returns {boolean} True if the view was found and the callback was registered.
 */
export function setOnActionRequested(id, callback) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.onActionRequested = callback;
    return true;
}

/**
 * Get the thumbnail layout of the pages of the specified view: the thumbnail size in pixels, before rotation, and the
 * rotation of each page. Call it again after rotating pages or changing the thumbnail configuration.
 * @param {string} id View id.
 * @returns {{ pageNumber: number, width: number, height: number, rotation: number }[] | null} The layout in page order,
 * or null when the view has no document.
 */
export function getThumbnailLayout(id) {
    if (!views.has(id)) {
        console.error(`View not found for id '${id}'`);
        return null;
    }
    const layout = interop.GetThumbnailLayout(id);
    return layout ? JSON.parse(layout) : null;
}

/**
 * Get the thumbnail of a page of the specified view when it is already produced.
 * @param {string} id View id.
 * @param {number} pageNumber 1-based page number.
 * @returns {Uint8Array | null} The thumbnail PNG, or null when it has not been produced.
 */
export function getThumbnail(id, pageNumber) {
    if (!views.has(id)) {
        console.error(`View not found for id '${id}'`);
        return null;
    }
    return interop.GetThumbnail(id, pageNumber);
}

/**
 * Set whether the thumbnail of a page of the specified view is shown. While it is visible, the thumbnail is kept and its
 * PNG is passed to the `setOnThumbnailUpdated` callback when it is ready, at once when it is already produced, and again
 * whenever it is produced anew. A thumbnail that is no longer visible may be evicted.
 * @param {string} id View id.
 * @param {number} pageNumber 1-based page number.
 * @param {boolean} visible Whether the thumbnail is shown.
 * @returns {boolean} True if the view has a document with the page.
 */
export function setThumbnailVisible(id, pageNumber, visible) {
    if (!views.has(id)) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    return interop.SetThumbnailVisible(id, pageNumber, visible);
}

/**
 * Subscribe to thumbnail updates of the specified view. The callback receives `{ pageNumber, png }`
 * for each visible thumbnail that is ready.
 * @param {string} id View id.
 * @param {(update: { pageNumber: number, png: Uint8Array }) => void} callback Called with each update.
 * @returns {boolean} True if the view was found and the callback was registered.
 */
export function setOnThumbnailUpdated(id, callback) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.onThumbnailUpdated = callback;
    return true;
}

/**
 * Scroll the specified view to a page and view given in the page's PDF user space, on the next render.
 * Works for pages that have not been shown yet.
 * @param {string} id View id.
 * @param {{ pageNumber: number, fitType: string, left?: number, top?: number, right?: number, bottom?: number, zoom?: number }} destination
 * 1-based page number, a fit type of ISO 32000 Table 149 (`XYZ`, `Fit`, `FitH`, `FitV`, `FitR`, `FitB`, `FitBH`, `FitBV`),
 * the coordinates that fit type uses and, for `XYZ`, the zoom (`1` = 100%).
 * @returns {boolean} True if the view has a document and the destination was accepted.
 */
export function scrollToDestination(id, destination) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    const accepted = interop.ScrollToDestination(id, destination);
    view.requestRender();
    return accepted;
}

/**
 * Rotate one page of the specified view by the given degrees, keeping the current page in view.
 * @param {string} id View id.
 * @param {number} pageNumber 1-based page number.
 * @param {number} degrees Clockwise rotation in degrees, a multiple of 90, added to the page's current rotation.
 * @returns {boolean} True if the view has a document and the page was rotated.
 */
export function rotatePage(id, pageNumber, degrees) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    const applied = interop.RotatePage(id, pageNumber, degrees);
    view.requestRender();
    return applied;
}

/**
 * Rotate every page of the specified view by the given degrees, keeping the current page in view.
 * @param {string} id View id.
 * @param {number} degrees Clockwise rotation in degrees, a multiple of 90, added to each page's current rotation.
 * @returns {boolean} True if the view has a document and the pages were rotated.
 */
export function rotatePages(id, degrees) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    const applied = interop.RotatePages(id, degrees);
    view.requestRender();
    return applied;
}

/**
 * Set the automatic scaling mode of the specified view. The mode applies to every document loaded afterwards and
 * returns to `noAutoScale` when the user zooms; the current mode is reported as `autoScaleMode` in the view state.
 * @param {string} id View id.
 * @param {'noAutoScale' | 'scaleToWidth' | 'scaleToHeight'} mode The automatic scaling mode.
 * @returns {boolean} True if the view was found and the mode was set.
 */
export function setAutoScaleMode(id, mode) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.state.autoScaleMode = mode;
    view.requestRender();
    return true;
}

/**
 * Request a redraw for the specified view.
 * @param {string} id View id.
 * @returns {boolean} True if the view was found and rendered.
 */
export function requestRedraw(id) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.requestRender();
    return true;
}

/**
 * Navigate to a specific page in the specified view.
 * Sets forcePageSet on the view state and requests a redraw.
 * @param {string} id View id.
 * @param {number} pageNumber 1-based page number to navigate to.
 * @returns {boolean} True if the view was found and navigation was requested.
 */
export function setPage(id, pageNumber) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.state.forcePageSet = pageNumber;
    view.requestRender();
    return true;
}

/**
 * Subscribe to state change notifications for the specified view.
 * The callback receives a snapshot of the full view state after every completed render.
 * @param {string} id View id.
 * @param {(state: object) => void} callback Called with the current state snapshot.
 * @returns {boolean} True if the view was found and the callback was registered.
 */
export function setOnStateChanged(id, callback) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.onStateChanged = callback;
    return true;
}

/**
 * Set the text to search for in the specified view, or `null` to stop searching.
 * The results arrive in the view state as `searchResults` (`{ pageNumber, startIndex, length, bounds }`, ordered by page),
 * with `isTextExtracted` set once the words of every page are extracted. Every page is searched only while
 * `extractAllText` is set through `updateConfiguration`.
 * @param {string} id View id.
 * @param {string | null} query Text to search for.
 * @returns {boolean} True if the view was found and the query was set.
 */
export function setSearchQuery(id, query) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.state.searchQuery = query || null;
    view.requestRender();
    return true;
}

/**
 * Set the search result highlighted as current in the specified view and scroll to it, or `null` for none.
 * @param {string} id View id.
 * @param {{ pageNumber: number, startIndex: number, length: number } | null} result A result from `searchResults`.
 * @returns {boolean} True if the view was found and the current result was set.
 */
export function setCurrentSearchResult(id, result) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.state.currentSearchResult = result
        ? { pageNumber: result.pageNumber, startIndex: result.startIndex, length: result.length }
        : null;
    view.requestRender();
    return true;
}

/**
 * Select the search result after the current one in the specified view, wrapping to the first,
 * or the first result on or after the current page when none is current.
 * @param {string} id View id.
 * @returns {boolean} True if the view was found.
 */
export function nextSearchResult(id) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.selectSearchResult(1);
    return true;
}

/**
 * Select the search result before the current one in the specified view, wrapping to the last,
 * or the first result on or after the current page when none is current.
 * @param {string} id View id.
 * @returns {boolean} True if the view was found.
 */
export function previousSearchResult(id) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.selectSearchResult(-1);
    return true;
}

/**
 * Update the configuration of the specified view. Only the keys that are set change; the others keep their current values.
 * `useWebGL` and `yieldInterval` apply only to the configuration passed to `registerPanel`.
 * @param {string} id View id.
 * @param {object} configuration `scrollStep` and the properties of `PdfPanelConfiguration` in camelCase.
 * @returns {boolean} True if the view was found and the configuration was updated.
 */
export function updateConfiguration(id, configuration) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.updateConfiguration(configuration || {});
    return true;
}

/**
 * Subscribe to frame notifications for the specified view.
 * The callback receives `{ id, canvas, frame }` after every presented frame: the view id, the overlay canvas the panel
 * adds to the container above its own canvas, sized to the frame in panel pixels, and the frame, which mirrors
 * `PdfPanelFrame` (`panelSize`, `hostToPanel` as a `DOMMatrix`, `pages`).
 * @param {string} id View id.
 * @param {(args: { id: string, canvas: HTMLCanvasElement, frame: object }) => void} callback Called after every present.
 * @returns {boolean} True if the view was found and the callback was registered.
 */
export function setOnFramePresented(id, callback) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.onFramePresented = callback;
    return true;
}

/**
 * Set the zoom scale for the specified view, keeping the viewport center fixed.
 * @param {string} id View id.
 * @param {number} scale The desired scale factor (e.g. 1.0 = 100%).
 * @returns {boolean} True if the view was found and the scale was updated.
 */
export function setScale(id, scale) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.state.zoomRequest = { scale: scale, centerX: view.state.panelWidth / 2, centerY: view.state.panelHeight / 2 };
    view.requestRender();
    return true;
}

/**
 * Passes a presented frame to the subscriber of the view.
 * Called from C# via JSImport.
 * @param {string} id View id.
 * @param {string} frameJson The frame `{ panelWidth, panelHeight, hostToPanel, pages }` as JSON, the matrices as
 * `[a, b, c, d, e, f]`.
 */
export function framePresented(id, frameJson) {
    const view = views.get(id);
    if (!view) {
        return;
    }

    const frame = JSON.parse(frameJson);

    view.presentFrame({
        panelSize: { width: frame.panelWidth, height: frame.panelHeight },
        hostToPanel: new DOMMatrix(frame.hostToPanel),
        pages: frame.pages.map(page => new PdfPanelFramePage(page))
    });
}

/**
 * Passes a thumbnail update to the subscriber of the view.
 * Called from C# via JSImport.
 * @param {string} id View id.
 * @param {number} pageNumber 1-based page number.
 * @param {Uint8Array} png The thumbnail PNG.
 */
export function thumbnailUpdated(id, pageNumber, png) {
    const view = views.get(id);
    if (view) {
        view.updateThumbnail({ pageNumber: pageNumber, png: png });
    }
}

/**
 * Requests a render of the specified view on the next animation frame.
 * Called from C# via JSImport.
 * @param {string} id View id.
 */
export function scheduleRedraw(id) {
    const view = views.get(id);
    if (view) {
        view.scheduleRender();
    }
}

/**
 * Creates an empty object.
 * Called from C# via JSImport.
 * @returns {object} The object.
 */
export function createObject() {
    return {};
}
