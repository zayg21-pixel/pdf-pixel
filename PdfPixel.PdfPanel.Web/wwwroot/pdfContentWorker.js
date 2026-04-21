import { dotnet } from './_framework/dotnet.js';
let workerInterop = null;

onmessage = async (e) => {
    console.log("Message received from main script", e.data);
    if (e.data[1] == 'initialize') {

    const { setModuleImports, getAssemblyExports } = await dotnet
        .withDiagnosticTracing(false)
        //.withApplicationArgumentsFromQuery()
        .create();

        setModuleImports('pdfContentWorker.js', self);

        const exports = await getAssemblyExports(`PdfPixel.PdfPanel.Web`);
        self.workerInterop = exports.PdfPixel.PdfPanel.Web.PdfContentWorkerInterop;

        self.workerInterop.Initialize();
        postMessage([null, 'initialized', null, null]);
    }
    else {
        let state = {
            id: e.data[0],
            message: e.data[1],
            parameters: e.data[2],
            data: e.data[3]
        };

        if (!self.workerInterop) {
            console.error("Worker interop is not initialized");
        }

        self.workerInterop.ProcessMessage(state);
        postMessage([state.id, state.message, state.parameters, state.data]);
    }

  //const workerResult = `Result: ${e.data[0] * e.data[1]}`;
  //console.log("Posting message back to main script");
  //postMessage(workerResult);
};