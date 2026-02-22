const views = new Map();
let interop = null;

class PdfPanelView {
    constructor(id, containerElement, configuration) {
        this.id = id;
        this.container = containerElement;
        this.canvas = containerElement.querySelector('.pdf-panel-canvas');
        this.scrollHost = containerElement.querySelector('.pdf-panel-scroll-host');
        this.spacer = this.scrollHost ? this.scrollHost.querySelector('.pdf-panel-scroll-spacer') : null;

        if (!this.canvas || !this.scrollHost || !this.spacer) {
            throw new Error(`Container structure is invalid for id '${id}'`);
        }

        const defaults = {
            zoomFactor: 0.1,
            minZoom: 0.1,
            maxZoom: 5.0,
            backgroundColor: '#D3D3D3',
            maxThumbnailSize: 400,
            pagesPadding: { left: 10, top: 10, right: 10, bottom: 10 },
            minimumPageGap: 10,
            scrollStep: 20
        };
        this.configuration = Object.assign({}, defaults, configuration || {});

        this.state = {
            verticalOffset: 0,
            horizontalOffset: 0,
            scale: 1.0,
            scrollWidth: 0,
            scrollHeight: 0,
            viewportWidth: 0,
            viewportHeight: 0,
            containerWidth: 0,
            containerHeight: 0,
            devicePixelScale: 1,
            mouseX: null,
            mouseY: null,
            currentPage: 0,
            pageCount: 0,
            forcePageSet: 0,
            pointerPressed: false
        };

        this.renderFrameRequestId = null;
        this.requestedRender = false;
        this.renderInProgress = false;
        this.renderVersion = 0;

        // Tracks the scroll position we set programmatically so onScroll can
        // ignore those events and only react to genuine user-initiated scrolls.
        this._expectedScrollLeft = 0;
        this._expectedScrollTop = 0;
        this.onStateChanged = null;

        this.onWheel = this.onWheel.bind(this);
        this.onScroll = this.onScroll.bind(this);
        this.onMouseMove = this.onMouseMove.bind(this);
        this.onResizeRequested = this.onResizeRequested.bind(this);
        this.onPointerDown = this.onPointerDown.bind(this);
        this.onPointerUp = this.onPointerUp.bind(this);
    }

    requestRender() {
        this.renderVersion = this.renderVersion + 1;
        this.requestedRender = true;

        if (this.renderFrameRequestId !== null) {
            return;
        }

        this.renderFrameRequestId = window.requestAnimationFrame(() => {
            this.renderFrameRequestId = null;
            void this.performRender();
        });
    }

    async performRender() {
        if (this.renderInProgress) {
            return;
        }
        if (!this.requestedRender) {
            return;
        }
        this.requestedRender = false;
        this.renderInProgress = true;

        const currentRenderVersion = this.renderVersion + 1;
        this.renderVersion = currentRenderVersion;

        try {
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

            const redrawState = {
                containerWidth: containerWidth,
                containerHeight: containerHeight,
                viewportWidth: physicalWidth,
                viewportHeight: physicalHeight,
                devicePixelScale: devicePixelScale,
                verticalOffset: this.state.verticalOffset,
                horizontalOffset: this.state.horizontalOffset,
                scale: this.state.scale,
                scrollWidth: 0,
                scrollHeight: 0,
                forcePageSet: this.state.forcePageSet,
                pointerInside: pointerInside,
                pointerX: pointerX,
                pointerY: pointerY,
                pointerPressed: this.state.pointerPressed
            };

            await interop.RequestRedraw(this.id, redrawState);

            this.state.forcePageSet = 0;

            if (currentRenderVersion !== this.renderVersion) {
                return;
            }

            this.state.containerWidth = redrawState.containerWidth;
            this.state.containerHeight = redrawState.containerHeight;
            this.state.devicePixelScale = redrawState.devicePixelScale;
            this.state.viewportWidth = redrawState.viewportWidth;
            this.state.viewportHeight = redrawState.viewportHeight;
            this.state.scrollWidth = redrawState.scrollWidth;
            this.state.scrollHeight = redrawState.scrollHeight;
            this.state.verticalOffset = redrawState.verticalOffset;
            this.state.horizontalOffset = redrawState.horizontalOffset;
            this.state.currentPage = redrawState.currentPage;
            this.state.pageCount = redrawState.pageCount;

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

        } finally {
            this.renderInProgress = false;
            if (this.requestedRender) {
                this.requestRender();
            }
        }
    }

    onWheel(e) {
        if (!e.ctrlKey) {
            return;
        }

        e.preventDefault();

        const oldScale = this.state.scale;
        const zoomDelta = this.configuration.zoomFactor;
        const nextScaleRequest = e.deltaY > 0 ? oldScale * (1 - zoomDelta) : oldScale * (1 + zoomDelta);
        const nextScale = Math.max(this.configuration.minZoom, Math.min(this.configuration.maxZoom, nextScaleRequest));

        // Compute center coordinates for zoom from mouse if available; otherwise center
        let centerX = this.state.viewportWidth / 2;
        let centerY = this.state.viewportHeight / 2;

        if (this.state.mouseX !== null && this.state.mouseY !== null) {
            centerX = this.state.mouseX * this.state.devicePixelScale;
            centerY = this.state.mouseY * this.state.devicePixelScale;
        }

        // Update offsets to keep zoom centered around pointer (or center)
        this.state.verticalOffset = (this.state.verticalOffset + centerY) * (nextScale / oldScale) - centerY;
        this.state.horizontalOffset = (this.state.horizontalOffset + centerX) * (nextScale / oldScale) - centerX;

        this.state.scale = nextScale;

        this.requestRender();
    }

    onScroll() {
        // Suppress scroll events that were fired by our own programmatic
        // scrollLeft/scrollTop assignments inside performRender().
        if (this.scrollHost.scrollLeft === this._expectedScrollLeft &&
            this.scrollHost.scrollTop === this._expectedScrollTop) {
            return;
        }

        const dpr = this.state.devicePixelScale || window.devicePixelRatio || 1;
        this.state.verticalOffset = this.scrollHost.scrollTop * dpr;
        this.state.horizontalOffset = this.scrollHost.scrollLeft * dpr;
        this.requestRender();
    }

    onMouseMove(e) {
        // Track mouse position relative to the scroll host for zoom-centering.
        const rect = this.scrollHost.getBoundingClientRect();
        const insideViewport =
            e.clientX >= rect.left &&
            e.clientX <= rect.right &&
            e.clientY >= rect.top &&
            e.clientY <= rect.bottom;

        if (insideViewport) {
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

    onResizeRequested() {
        const hasHorizontalScrollbar = this.state.scrollWidth > this.state.viewportWidth;
        const hasVerticalScrollbar = this.state.scrollHeight > this.state.viewportHeight;

        this.scrollHost.style.overflowX = hasHorizontalScrollbar ? 'scroll' : 'hidden';
        this.scrollHost.style.overflowY = hasVerticalScrollbar ? 'scroll' : 'hidden';

        this.requestRender();
    }

    attachEvents() {
        this.container.addEventListener('wheel', this.onWheel, { passive: false });
        this.scrollHost.addEventListener('scroll', this.onScroll);
        this.scrollHost.addEventListener('pointerdown', this.onPointerDown);
        document.addEventListener('mousemove', this.onMouseMove);
        document.addEventListener('pointerup', this.onPointerUp);

        this.resizeObserver = new ResizeObserver(this.onResizeRequested);
        this.resizeObserver.observe(this.container);
    }

    detachEvents() {
        this.container.removeEventListener('wheel', this.onWheel);
        this.scrollHost.removeEventListener('scroll', this.onScroll);
        this.scrollHost.removeEventListener('pointerdown', this.onPointerDown);
        document.removeEventListener('mousemove', this.onMouseMove);
        document.removeEventListener('pointerup', this.onPointerUp);

        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            this.resizeObserver = null;
        }
    }

    async initInterop() {
        const thumbnailSize = this.configuration.maxThumbnailSize || 400;
        this.thumbnailCanvas = document.createElement('canvas');
        this.thumbnailCanvas.classList.add('pdf-thumbnail-canvas');
        this.thumbnailCanvas.width = thumbnailSize;
        this.thumbnailCanvas.height = thumbnailSize;
        this.thumbnailCanvas.style.cssText = 'position:fixed; visibility:hidden; pointer-events:none;';
        this.container.appendChild(this.thumbnailCanvas);

        await interop.RegisterCanvas(this.id, this.configuration);
    }

    async start() {
        this.container.style.backgroundColor = this.configuration.backgroundColor;
        this.attachEvents();
        await this.initInterop();
        this.requestRender();
        console.log(`View '${this.id}' registered successfully`);
    }

    dispose() {
        this.detachEvents();
        if (this.thumbnailCanvas) {
            this.thumbnailCanvas.remove();
            this.thumbnailCanvas = null;
        }
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
    await interop.Initialize();

    console.log('Panel interop initialized');
}

/**
 * Register a PDF panel view bound to a container element.
 * @param {string} id Unique view id.
 * @param {HTMLElement} containerElement The `.pdf-panel-*` container element.
 * @returns {Promise<boolean>} True if registration succeeded.
 */
export async function registerPanel(id, containerElement, configuration) {
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
    await view.start();
    return true;
}

/**
 * Unregister and dispose a PDF panel view.
 * @param {string} id View id to unregister.
 * @returns {Promise<boolean>} True if unregistered.
 */
export async function unregisterPanel(id) {
    if (!views.has(id)) {
        console.warn(`Canvas with id '${id}' is not registered`);
        return false;
    }
    const view = views.get(id);
    if (view) {
        view.dispose();
    }
    views.delete(id);
    console.log(`Canvas '${id}' unregistered successfully`);
    await interop.UnregisterCanvas(id);
    return true;
}

/**
 * Register font data for a standard PDF font by name.
 * The name must match a PdfStandardFontName enum value (e.g. "Times", "Helvetica", "Courier").
 * Must be called before loading documents that use the font.
 * @param {string} name Standard font name (case-insensitive).
 * @param {Uint8Array} fontData Raw font file bytes (TTF, OTF, etc.).
 * @returns {Promise<void>} Resolves when the font is registered.
 */
export async function setFont(name, fontData) {
    interop.SetFont(name, fontData);
}

/**
 * Set the PDF document for the specified view.
 * @param {string} id View id.
 * @param {Uint8Array} documentData PDF file bytes.
 * @returns {Promise<void>} Resolves when the document is set.
 */
export async function setDocument(id, documentData) {
    await interop.SetDocument(id, documentData);
}

/**
 * Request a redraw for the specified view.
 * @param {string} id View id.
 * @returns {boolean} True if the view was found and a render was enqueued.
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
    const clampedScale = Math.max(
        view.configuration.minZoom,
        Math.min(view.configuration.maxZoom, scale)
    );
    const oldScale = view.state.scale;
    const centerX = view.state.viewportWidth / 2;
    const centerY = view.state.viewportHeight / 2;
    view.state.verticalOffset = (view.state.verticalOffset + centerY) * (clampedScale / oldScale) - centerY;
    view.state.horizontalOffset = (view.state.horizontalOffset + centerX) * (clampedScale / oldScale) - centerX;
    view.state.scale = clampedScale;
    view.requestRender();
    return true;
}
