import { dotnet } from './_framework/dotnet.js';

onmessage = async (e) => {
    if (e.data[1] == 'Initialize') {

    const { setModuleImports, getAssemblyExports } = await dotnet
        .withDiagnosticTracing(false)
        .create();

        self.onDataReady = onDataReady;
        setModuleImports('pdfContentWorker.js', self);

        const exports = await getAssemblyExports(`PdfPixel.PdfPanel.Web`);
        self.workerInterop = exports.PdfPixel.PdfPanel.Web.PdfContentWorkerInterop;

        self.workerInterop.Initialize();
        postMessage([null, 'Initialize', null, null]);
    }
    else {
        if (!self.workerInterop) {
            console.error("Worker interop is not initialized");
        }

       self.workerInterop.ProcessMessage(e.data[0], e.data[1], e.data[2], e.data[3]);
    }
};

export function onDataReady(id, commandType, header, response) {
    postMessage([id, commandType, header, response]);
}