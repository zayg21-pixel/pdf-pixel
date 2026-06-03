import { dotnet } from './_framework/dotnet.js';

onmessage = async (e) => {
    const containerId = e.data[0];
    const id = e.data[1];
    const commandType = e.data[2];

    if (commandType == 'Initialize') {

    const { setModuleImports, getAssemblyExports } = await dotnet
        .withDiagnosticTracing(false)
        .create();

        self.onDataReady = onDataReady;
        setModuleImports('pdfContentWorker.js', self);

        const exports = await getAssemblyExports(`PdfPixel.PdfPanel.Web`);
        self.workerInterop = exports.PdfPixel.PdfPanel.Web.PdfContentWorkerInterop;

        self.workerInterop.Initialize();
        postMessage([null, null, 'Initialize', null, null]);
    }
    else {
        const sab = e.data[5];
        if (sab instanceof SharedArrayBuffer) {
            if (!globalThis.workerRequestSabMap) globalThis.workerRequestSabMap = {};
            globalThis.workerRequestSabMap[id] = new Int8Array(sab);
        }

        if (!self.workerInterop) {
            console.error("Worker interop is not initialized");
        }

        self.workerInterop.ProcessMessage(containerId, id, commandType, e.data[3], e.data[4]);
    }
};

export function onDataReady(containerId, id, commandType, header, response) {
    postMessage([containerId, id, commandType, header, response]);
}
