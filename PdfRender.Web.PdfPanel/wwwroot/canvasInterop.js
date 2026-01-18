const canvases = new Map();

export function registerCanvas(id, canvasElement) {
    if (!canvasElement) {
        console.error(`Canvas element is null or undefined for id '${id}'`);
        return false;
    }

    if (canvases.has(id)) {
        console.warn(`Canvas with id '${id}' is already registered`);
        return false;
    }

    const ctx = canvasElement.getContext('2d', { alpha: false, willReadFrequently: true });
    if (!ctx) {
        console.error(`Failed to get 2D context for canvas '${id}'`);
        return false;
    }

    ctx.imageSmoothingEnabled = false;
    ctx.mozImageSmoothingEnabled = false;
    ctx.webkitImageSmoothingEnabled = false;
    ctx.msImageSmoothingEnabled = false;

    canvasElement.style.imageRendering = 'pixelated';
    canvasElement.style.imageRendering = '-moz-crisp-edges';
    canvasElement.style.imageRendering = 'crisp-edges';

    canvases.set(id, { canvas: canvasElement, ctx: ctx });
    console.log(`Canvas '${id}' registered successfully`);
    return true;
}

export function unregisterCanvas(id) {
    if (!canvases.has(id)) {
        console.warn(`Canvas with id '${id}' is not registered`);
        return false;
    }

    canvases.delete(id);
    console.log(`Canvas '${id}' unregistered successfully`);
    return true;
}

export function renderRgbaToCanvas(id, width, height, rgbaBytes) {
    const entry = canvases.get(id);
    if (!entry || !entry.canvas || !entry.ctx) {
        console.error(`Canvas or context not initialized for id '${id}'`);
        return false;
    }

    if (!rgbaBytes) {
        console.error('No pixel buffer provided');
        return false;
    }

    if (width !== entry.canvas.width || height !== entry.canvas.height) {
        console.trace(`Dimension mismatch: render ${width}x${height}, canvas ${entry.canvas.width}x${entry.canvas.height}`); // TODO: this seems to be ok, we can safely ignore intermediate requests
        return false;
    }

    const ctx = entry.ctx;
    const imageData = new ImageData(new Uint8ClampedArray(rgbaBytes.slice()), width, height);
    ctx.putImageData(imageData, 0, 0);
    return true;
}

export function getCanvasWidth(id) {
    const entry = canvases.get(id);
    if (!entry || !entry.canvas) {
        console.error(`Canvas not initialized for id '${id}'`);
        return 0;
    }
    return entry.canvas.width;
}

export function getCanvasHeight(id) {
    const entry = canvases.get(id);
    if (!entry || !entry.canvas) {
        console.error(`Canvas not initialized for id '${id}'`);
        return 0;
    }
    return entry.canvas.height;
}

export function resizeCanvas(id, cssWidth, cssHeight, effectiveScale) {
    const entry = canvases.get(id);
    if (!entry || !entry.canvas || !entry.ctx) {
        console.error(`Canvas or context not initialized for id '${id}'`);
        return false;
    }

    const canvas = entry.canvas;
    const ctx = entry.ctx;
    const physicalWidth = Math.round(cssWidth * effectiveScale);
    const physicalHeight = Math.round(cssHeight * effectiveScale);

    const oldWidth = canvas.width;
    const oldHeight = canvas.height;

    if (oldHeight === physicalHeight && oldWidth === physicalWidth) {
        return true; // No resize needed
    };

    let snapshot = null;

    // Save current canvas content if it has been rendered
    if (oldWidth > 0 && oldHeight > 0) {
        snapshot = ctx.getImageData(0, 0, oldWidth, oldHeight);
    }

    // Resize canvas
    canvas.width = physicalWidth;
    canvas.height = physicalHeight;

    canvas.style.width = cssWidth + 'px';
    canvas.style.height = cssHeight + 'px';

    // Clear with white background
    ctx.fillStyle = 'white';
    ctx.fillRect(0, 0, physicalWidth, physicalHeight);
    ctx.putImageData(snapshot, 0, 0);
    return true;
}
