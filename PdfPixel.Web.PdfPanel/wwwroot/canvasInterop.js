const views = new Map();
let interop = null;

class PdfPanelView {
    constructor(id, containerElement, configuration) {
        this.id = id;
        this.container = containerElement;
        this.canvas = containerElement.querySelector('.pdf-panel-canvas');
        this.viewport = containerElement.querySelector('.pdf-panel-viewport');
        this.verticalScrollbar = containerElement.querySelector('.pdf-panel-vertical-scrollbar');
        this.horizontalScrollbar = containerElement.querySelector('.pdf-panel-horizontal-scrollbar');
        this.verticalThumb = this.verticalScrollbar ? this.verticalScrollbar.querySelector('.pdf-panel-scrollbar-thumb') : null;
        this.horizontalThumb = this.horizontalScrollbar ? this.horizontalScrollbar.querySelector('.pdf-panel-scrollbar-thumb') : null;

        if (!this.canvas || !this.viewport || !this.verticalScrollbar || !this.horizontalScrollbar || !this.verticalThumb || !this.horizontalThumb) {
            throw new Error(`Container structure is invalid for id '${id}'`);
        }

        const defaults = {
            zoomFactor: 0.1,
            minZoom: 0.1,
            maxZoom: 5.0,
            scrollStep: 2,
            backgroundColor: '#D3D3D3',
            maxThumbnailSize: 400,
            pagesPadding: { left: 10, top: 10, right: 10, bottom: 10 },
            minimumPageGap: 10
        };
        this.configuration = Object.assign({}, defaults, configuration || {});

        const ctx = this.canvas.getContext('2d', { alpha: false, willReadFrequently: true });
        if (!ctx) {
            throw new Error(`Failed to get 2D context for canvas '${id}'`);
        }
        this.ctx = ctx;

        this.ctx.imageSmoothingEnabled = false;
        this.ctx.mozImageSmoothingEnabled = false;
        this.ctx.webkitImageSmoothingEnabled = false;
        this.ctx.msImageSmoothingEnabled = false;

        // Initial background fill
        this.ctx.fillStyle = this.configuration.backgroundColor;
        this.ctx.fillRect(0, 0, this.canvas.width || 0, this.canvas.height || 0);

        this.canvas.style.imageRendering = 'pixelated';
        this.canvas.style.imageRendering = '-moz-crisp-edges';
        this.canvas.style.imageRendering = 'crisp-edges';

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
            isDraggingVertical: false,
            isDraggingHorizontal: false,
            dragStartY: 0,
            dragStartX: 0,
            dragStartOffset: 0,
            mouseX: null,
            mouseY: null
        };

        this.renderFrameRequestId = null;
        this.requestedRender = false;
        this.renderInProgress = false;
        this.renderVersion = 0;

        this.onWheel = this.onWheel.bind(this);
        this.onMouseMove = this.onMouseMove.bind(this);
        this.onMouseUp = this.onMouseUp.bind(this);
        this.onVerticalThumbMouseDown = this.onVerticalThumbMouseDown.bind(this);
        this.onHorizontalThumbMouseDown = this.onHorizontalThumbMouseDown.bind(this);
        this.onResizeRequested = this.onResizeRequested.bind(this);
    }

    getBrowserZoom() {
        if (window.visualViewport && window.visualViewport.scale) {
            return window.visualViewport.scale;
        }
        return 1;
    }

    updateScrollbars() {
        const cssWidth = this.state.containerWidth;
        const cssHeight = this.state.containerHeight;
        const canvasWidth = this.state.viewportWidth;
        const canvasHeight = this.state.viewportHeight;

        const verticalVisible = this.state.scrollHeight > canvasHeight;
        const horizontalVisible = this.state.scrollWidth > canvasWidth;

        this.container.classList.toggle('has-vertical-scrollbar', verticalVisible);
        this.container.classList.toggle('has-horizontal-scrollbar', horizontalVisible);

        if (verticalVisible) {
            const thumbHeight = Math.max(30, (canvasHeight / this.state.scrollHeight) * cssHeight);
            const maxThumbTop = cssHeight - thumbHeight;
            const maxScroll = this.state.scrollHeight - canvasHeight;
            const thumbTop = maxScroll > 0 ? (this.state.verticalOffset / maxScroll) * maxThumbTop : 0;

            this.verticalThumb.style.height = thumbHeight + 'px';
            this.verticalThumb.style.top = thumbTop + 'px';
        }

        if (horizontalVisible) {
            const thumbWidth = Math.max(30, (canvasWidth / this.state.scrollWidth) * cssWidth);
            const maxThumbLeft = cssWidth - thumbWidth;
            const maxScroll = this.state.scrollWidth - canvasWidth;
            const thumbLeft = maxScroll > 0 ? (this.state.horizontalOffset / maxScroll) * maxThumbLeft : 0;

            this.horizontalThumb.style.width = thumbWidth + 'px';
            this.horizontalThumb.style.left = thumbLeft + 'px';
        }
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
            const containerWidth = this.viewport.clientWidth;
            const containerHeight = this.viewport.clientHeight;

            const dpr = window.devicePixelRatio || 1;
            const zoom = this.getBrowserZoom();
            const devicePixelScale = dpr * zoom;

            this.resizeCanvas(containerWidth, containerHeight, devicePixelScale);

            const redrawState = {
                containerWidth: containerWidth,
                containerHeight: containerHeight,
                viewportWidth: this.canvas.width,
                viewportHeight: this.canvas.height,
                devicePixelScale: devicePixelScale,
                verticalOffset: this.state.verticalOffset,
                horizontalOffset: this.state.horizontalOffset,
                scale: this.state.scale,
                scrollWidth: 0,
                scrollHeight: 0
            };

            await interop.RequestRedraw(this.id, redrawState);

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

            this.updateScrollbars();

        } finally {
            this.renderInProgress = false;
            if (this.requestedRender) {
                this.requestRender();
            }
        }
    }

    resizeCanvas(containerWidth, containerHeight, devicePixelScale) {
        const physicalWidth = Math.round(containerWidth * devicePixelScale);
        const physicalHeight = Math.round(containerHeight * devicePixelScale);

        const oldWidth = this.canvas.width;
        const oldHeight = this.canvas.height;
        if (oldWidth === physicalWidth && oldHeight === physicalHeight) {
            return true;
        }

        let snapshot = null;
        if (oldWidth > 0 && oldHeight > 0) {
            snapshot = this.ctx.getImageData(0, 0, oldWidth, oldHeight);
        }

        this.canvas.width = physicalWidth;
        this.canvas.height = physicalHeight;

        this.canvas.style.width = containerWidth + 'px';
        this.canvas.style.height = containerHeight + 'px';

        this.ctx.fillStyle = this.configuration.backgroundColor;
        this.ctx.fillRect(0, 0, physicalWidth, physicalHeight);
        if (snapshot) {
            this.ctx.putImageData(snapshot, 0, 0);
        }
        return true;
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

            if (this.state.mouseX && this.state.mouseY) {
                centerX = this.state.mouseX * this.state.devicePixelScale;
                centerY = this.state.mouseY * this.state.devicePixelScale;
            }

            // Update offsets to keep zoom centered around pointer (or center)
            this.state.verticalOffset = (this.state.verticalOffset + centerY) * (nextScale / oldScale) - centerY;
            this.state.horizontalOffset = (this.state.horizontalOffset + centerX) * (nextScale / oldScale) - centerX;

            this.state.scale = nextScale;
        } else {
            this.state.verticalOffset = this.state.verticalOffset + e.deltaY * this.configuration.scrollStep;
            this.state.horizontalOffset = this.state.horizontalOffset + e.deltaX * this.configuration.scrollStep;
        }
        this.requestRender();
    }

    onMouseMove(e) {
        // Track mouse position relative to viewport for zoom-centering
        const rect = this.viewport.getBoundingClientRect();
        const insideViewport = e.clientX >= rect.left && e.clientX <= rect.right && e.clientY >= rect.top && e.clientY <= rect.bottom;
        if (insideViewport) {
            this.state.mouseX = e.clientX - rect.left;
            this.state.mouseY = e.clientY - rect.top;
        } else {
            this.state.mouseX = null;
            this.state.mouseY = null;
        }

        if (this.state.isDraggingVertical) {
            const cssHeight = this.state.containerHeight;
            const thumbHeight = parseFloat(this.verticalThumb.style.height);
            const maxThumbTop = cssHeight - thumbHeight;
            const maxScroll = this.state.scrollHeight - this.state.viewportHeight;

            const deltaY = e.clientY - this.state.dragStartY;
            const deltaScroll = maxThumbTop > 0 ? (deltaY / maxThumbTop) * maxScroll : 0;

            this.state.verticalOffset = this.state.dragStartOffset + deltaScroll;
            this.requestRender();
        }

        if (this.state.isDraggingHorizontal) {
            const cssWidth = this.state.containerWidth;
            const thumbWidth = parseFloat(this.horizontalThumb.style.width);
            const maxThumbLeft = cssWidth - thumbWidth;
            const maxScroll = this.state.scrollWidth - this.state.viewportWidth;

            const deltaX = e.clientX - this.state.dragStartX;
            const deltaScroll = maxThumbLeft > 0 ? (deltaX / maxThumbLeft) * maxScroll : 0;

            this.state.horizontalOffset = this.state.dragStartOffset + deltaScroll;
            this.requestRender();
        }
    }

    onMouseUp() {
        this.state.isDraggingVertical = false;
        this.state.isDraggingHorizontal = false;
    }

    onVerticalThumbMouseDown(e) {
        this.state.isDraggingVertical = true;
        this.state.dragStartY = e.clientY;
        this.state.dragStartOffset = this.state.verticalOffset;
        e.preventDefault();
    }

    onHorizontalThumbMouseDown(e) {
        this.state.isDraggingHorizontal = true;
        this.state.dragStartX = e.clientX;
        this.state.dragStartOffset = this.state.horizontalOffset;
        e.preventDefault();
    }

    onResizeRequested() {
        this.requestRender();
    }

    attachEvents() {
        this.container.addEventListener('wheel', this.onWheel, { passive: false });
        document.addEventListener('mousemove', this.onMouseMove);
        document.addEventListener('mouseup', this.onMouseUp);
        this.verticalThumb.addEventListener('mousedown', this.onVerticalThumbMouseDown);
        this.horizontalThumb.addEventListener('mousedown', this.onHorizontalThumbMouseDown);

        window.addEventListener('resize', this.onResizeRequested);
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', this.onResizeRequested);
        }

        this.resizeObserver = new ResizeObserver(this.onResizeRequested);
        this.resizeObserver.observe(this.container);
        this.resizeObserver.observe(this.viewport);
    }

    detachEvents() {
        this.container.removeEventListener('wheel', this.onWheel);
        document.removeEventListener('mousemove', this.onMouseMove);
        document.removeEventListener('mouseup', this.onMouseUp);
        this.verticalThumb.removeEventListener('mousedown', this.onVerticalThumbMouseDown);
        this.horizontalThumb.removeEventListener('mousedown', this.onHorizontalThumbMouseDown);

        window.removeEventListener('resize', this.onResizeRequested);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener('resize', this.onResizeRequested);
        }

        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            this.resizeObserver = null;
        }
    }

    async initInterop() {
        await interop.RegisterCanvas(this.id, this.configuration);
    }

    async start() {
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
 * @returns {Promise<void>} Resolves when interop is ready.
 */
export async function initialize(setModuleImports, getAssemblyExports) {
    const exports = await getAssemblyExports(`PdfPixel.Web.PdfPanel`);
    const panelInterop = exports.PdfPixel.Web.PdfPanel.PdfPanelIntrop;

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
 * Set the PDF document for the specified view.
 * @param {string} id View id.
 * @param {Uint8Array} documentData PDF file bytes.
 * @returns {Promise<void>} Resolves when the document is set.
 */
export async function setDocument(id, documentData) {
    await interop.SetDocument(id, documentData);
}

/**
 * Request a render for the specified view.
 * @param {string} id View id.
 * @returns {boolean} True if the view was found and a render was enqueued.
 */
export function refreshPanel(id) {
    const view = views.get(id);
    if (!view) {
        console.error(`View not found for id '${id}'`);
        return false;
    }
    view.requestRender();
    return true;
}

export function _renderRgbaToCanvas(id, width, height, rgbaBytes) {
    const entry = views.get(id);
    if (!entry || !entry.canvas || !entry.ctx) {
        console.error(`Canvas or context not initialized for id '${id}'`);
        return false;
    }

    if (!rgbaBytes) {
        console.error('No pixel buffer provided');
        return false;
    }

    if (width !== entry.canvas.width || height !== entry.canvas.height) {
        console.trace(`Dimension mismatch: render ${width}x${height}, canvas ${entry.canvas.width}x${entry.canvas.height}`);
        return false;
    }

    const ctx = entry.ctx;
    const imageData = new ImageData(new Uint8ClampedArray(rgbaBytes.slice()), width, height);
    ctx.putImageData(imageData, 0, 0);
    return true;
}