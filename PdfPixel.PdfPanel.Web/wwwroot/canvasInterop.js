const views = new Map();
let interop = null;
let emscriptenModule = null;

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
            pointerPressed: false,
            annotationPopup: null,
            cursorStyle: 'default'
        };

        this.renderFrameRequestId = null;
        this._rendering = false;

        // Tracks the scroll position we set programmatically so onScroll can
        // ignore those events and only react to genuine user-initiated scrolls.
        this._expectedScrollLeft = 0;
        this._expectedScrollTop = 0;
        this.onStateChanged = null;

        // Tracks the initial pinch distance and scale for two-finger zoom gestures.
        this._touchStartDistance = 0;
        this._touchStartScale = null;

        this.canvas.offscreenCanvas = this.canvas.transferControlToOffscreen();
        this.canvas.transferControlToOffscreen = true;

        this.onWheel = this.onWheel.bind(this);
        this.onScroll = this.onScroll.bind(this);
        this.onMouseMove = this.onMouseMove.bind(this);
        this.onResizeRequested = this.onResizeRequested.bind(this);
        this.onPointerDown = this.onPointerDown.bind(this);
        this.onPointerUp = this.onPointerUp.bind(this);
        this.onTouchStart = this.onTouchStart.bind(this);
        this.onTouchMove = this.onTouchMove.bind(this);
        this.onTouchEnd = this.onTouchEnd.bind(this);
    }

    requestRender() {
        this._dirty = true;

        if (this.renderFrameRequestId !== null || this._rendering) {
            return;
        }

        this.renderFrameRequestId = window.requestAnimationFrame(() => {
            this.renderFrameRequestId = null;
            void this.performRender();
        });
    }

    async performRender() {
        this._rendering = true;
        this._dirty = false;

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

            // Annotation handling: cursor, popup, and URI open
            this.state.annotationPopup = redrawState.annotationPopup || null;
            this.state.cursorStyle = redrawState.cursorStyle || 'default';
            this.scrollHost.style.cursor = this.state.cursorStyle;

            if (redrawState.openUri) {
                window.open(redrawState.openUri, '_blank', 'noopener,noreferrer');
            }

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
            this._rendering = false;
        }

        // If input arrived during the render, schedule a follow-up immediately.
        if (this._dirty) {
            this.requestRender();
        }
    }

    onWheel(e) {
        e.preventDefault();

        if (e.ctrlKey) {
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
        const newScale = Math.max(
            this.configuration.minZoom,
            Math.min(this.configuration.maxZoom, this._touchStartScale * scaleFactor)
        );

        const oldScale = this.state.scale;
        const midpoint = this._getTouchMidpoint(e.touches);
        const rect = this.scrollHost.getBoundingClientRect();
        const centerX = (midpoint.x - rect.left) * this.state.devicePixelScale;
        const centerY = (midpoint.y - rect.top) * this.state.devicePixelScale;

        this.state.verticalOffset = (this.state.verticalOffset + centerY) * (newScale / oldScale) - centerY;
        this.state.horizontalOffset = (this.state.horizontalOffset + centerX) * (newScale / oldScale) - centerX;
        this.state.scale = newScale;

        this.requestRender();
    }

    onTouchEnd(e) {
        if (e.touches.length < 2) {
            this._touchStartDistance = 0;
            this._touchStartScale = null;
        }
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
        this.scrollHost.addEventListener('touchstart', this.onTouchStart, { passive: false });
        this.scrollHost.addEventListener('touchmove', this.onTouchMove, { passive: false });
        this.scrollHost.addEventListener('touchend', this.onTouchEnd);
        document.addEventListener('mousemove', this.onMouseMove);
        document.addEventListener('pointerup', this.onPointerUp);

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

        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            this.resizeObserver = null;
        }
    }

    async initInterop() {
        const offscreenCanvas = this.canvas.offscreenCanvas;

        const canvasId = `#${this.id} .pdf-panel-canvas`;

        // Key for offscreenCanvases: strip leading '#'
        const canvasKey = canvasId.substring(1);

        if (offscreenCanvas && emscriptenModule && emscriptenModule.PThread) {
            const pThread = emscriptenModule.PThread;
            const originalLoadWasmModuleToWorker = pThread.loadWasmModuleToWorker.bind(pThread);

            pThread.loadWasmModuleToWorker = (worker, onFinishedLoading) => {

                const originalPrototypePostMessage = Worker.prototype.postMessage;
                let transferred = false;

                // Patch Worker.prototype.postMessage to add canvas to offscreenCanvases on 'run'
                Worker.prototype.postMessage = function(message, transfer) {
                    const transferList = transfer ? [...transfer] : [];

                    // Add canvas to the 'run' command's offscreenCanvases
                    if (!transferred && message && message.cmd === 'run') {
                        // Ensure offscreenCanvases exists
                        if (!message.offscreenCanvases) {
                            message.offscreenCanvases = {};
                        }
                        message.offscreenCanvases[canvasKey] = offscreenCanvas;
                        transferList.push(offscreenCanvas);
                        transferred = true;
                        console.log(`[PdfPanel] Added OffscreenCanvas to 'run' command, key: ${canvasKey}`);
                        // Restore original prototype after transfer
                        Worker.prototype.postMessage = originalPrototypePostMessage;
                    }

                    return originalPrototypePostMessage.call(this, message, transferList);
                };

                // Restore original loadWasmModuleToWorker after first worker
                pThread.loadWasmModuleToWorker = originalLoadWasmModuleToWorker;

                return originalLoadWasmModuleToWorker(worker, onFinishedLoading);
            };
        } else {
            console.warn('[PdfPanel] PThread not available:', {
                hasOffscreenCanvas: !!offscreenCanvas,
                hasEmscriptenModule: !!emscriptenModule,
                hasPThread: !!(emscriptenModule && emscriptenModule.PThread)
            });
        }


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
    }
}

/**
 * Initialize PDF panel interop and bind JS module imports.
 * @param {(name: string, module: any) => void} setModuleImports Binds a logical module name to an ESM object for [JSImport].
 * @param {(assemblyName: string) => Promise<any>} getAssemblyExports Retrieves .NET assembly exports.
 * @param {object} wasmModule The Emscripten Module object (captured via dotnet.withModuleConfig).
 * @returns {Promise<void>} Resolves when interop is ready.
 */
export async function initialize(setModuleImports, getAssemblyExports, wasmModule) {
    const exports = await getAssemblyExports(`PdfPixel.PdfPanel.Web`);
    const panelInterop = exports.PdfPixel.PdfPanel.Web.PdfPanelInterop;

    setModuleImports('canvasInterop.js', this);

    emscriptenModule = wasmModule;
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
    await interop.SetFont(name, fontData);
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

/**
 * Creates an annotation popup object on the state.
 * Called from C# via JSImport to build the popup as a JS object.
 * @param {object} state The redraw state object.
 * @param {string} type Annotation type name (e.g. "link", "annotation").
 * @param {boolean} isInteractive Whether the annotation is interactive.
 */
export function createAnnotationPopupState(state, type, isInteractive) {
    state.annotationPopup = {
        type: type,
        isInteractive: isInteractive,
        messages: []
    };
}

/**
 * Adds a message to the annotation popup on the state.
 * Called from C# via JSImport for each message in the annotation thread.
 * @param {object} state The redraw state object.
 * @param {string} title Message author/title.
 * @param {string} content Message text content.
 * @param {string} date ISO 8601 creation date, or empty string.
 */
export function addAnnotationPopupMessage(state, title, content, date) {
    if (state.annotationPopup) {
        state.annotationPopup.messages.push({
            title: title,
            content: content,
            date: date
        });
    }
}

/**
 * Clears the annotation popup from the state.
 * Called from C# via JSImport when no annotation is active.
 * @param {object} state The redraw state object.
 */
export function clearAnnotationPopupState(state) {
    state.annotationPopup = null;
}
