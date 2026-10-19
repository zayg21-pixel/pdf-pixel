import { dotnet } from './_framework/dotnet.js';
import * as canvasInterop from './canvasInterop.js';

let initialization = null;

/**
 * Start the .NET runtime and bind the PDF panel interop. Every call returns the promise of the first one.
 * @param {{ setModuleImports: Function, getAssemblyExports: Function }} [runtime] A runtime created with the exported
 * `dotnet` builder, for a custom runtime configuration. When omitted, a runtime with the default configuration is created.
 * @returns {Promise<void>} Resolves when the panel functions can be used.
 */
export function initialize(runtime) {
    if (initialization === null) {
        initialization = startRuntime(runtime);
    }

    return initialization;
}

async function startRuntime(runtime) {
    const { setModuleImports, getAssemblyExports } = runtime ?? await dotnet.create();
    await canvasInterop.initialize(setModuleImports, getAssemblyExports);
}

/**
 * Resource loader for `dotnet.withResourceLoader` that downloads the gzip-compressed copy (`.gz`) of each runtime
 * resource and decompresses it in the browser, for hosts that cannot serve compressed responses.
 * JavaScript modules, and every resource when the browser has no `DecompressionStream`, load uncompressed.
 * @param {string} type Resource type reported by the runtime.
 * @param {string} name Resource name.
 * @param {string} defaultUri Address of the uncompressed resource.
 * @returns {Promise<Response> | undefined} The decompressed resource, or undefined to load it uncompressed.
 */
export function loadGzipResource(type, name, defaultUri) {
    if (type === 'dotnetjs' || typeof DecompressionStream === 'undefined') {
        return undefined;
    }

    const compressedUri = new URL(defaultUri, document.baseURI);
    compressedUri.pathname += '.gz';

    return fetch(compressedUri).then(response => {
        if (!response.ok) {
            return fetch(defaultUri);
        }

        const body = (response.headers.get('content-encoding') === 'gzip')
            ? response.body
            : response.body.pipeThrough(new DecompressionStream('gzip'));
        const contentType = compressedUri.pathname.endsWith('.wasm.gz') ? 'application/wasm' : 'application/octet-stream';

        return new Response(body, { headers: { 'content-type': contentType } });
    });
}

export { dotnet };

export {
    registerPanel,
    unregisterPanel,
    setDocument,
    requestRedraw,
    setPage,
    setOnStateChanged,
    setSearchQuery,
    setCurrentSearchResult,
    nextSearchResult,
    previousSearchResult,
    updateConfiguration,
    setOnFramePresented,
    setScale,
    scrollToDestination,
    setPageRotation,
    setRotation,
    setAutoScaleMode,
    activateOutlineItem,
    setLayerConfiguration,
    setLayerVisible,
    getFileBytes,
    setOnActionRequested,
    setOnCredentialRequested,
    requestThumbnail,
    cancelThumbnail,
    setOnThumbnailUpdated
} from './canvasInterop.js';
