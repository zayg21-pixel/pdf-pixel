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
    setScale
} from './canvasInterop.js';
