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
    else if (e.data[1] == 'TestSharedArrayBuffer') {
        if (!self.workerInterop) {
            console.error("Worker interop is not initialized");
            return;
        }
        // Normalize whatever main thread sent (Int32Array view, Uint8Array view, or
        // WebAssembly.Memory) into a SAB-backed Int32Array so it matches the
        // [JSMarshalAs<JSType.MemoryView>] Span<int> JSExport signature.
        let payload = e.data[3];
        let sab;
        if (payload instanceof WebAssembly.Memory) {
            sab = payload.buffer;
        } else if (ArrayBuffer.isView(payload)) {
            sab = payload.buffer;
        } else {
            sab = payload;
        }
        const int32View = new Int32Array(sab);
        // Stash a SAB-backed view on the worker global so the EM_JS function
        // dotnet_test_read_shared_byte (Atomics.load) can read the same memory.
        // The JSExport itself takes no arguments — all data flows through this view.
        self.testSharedView = int32View;
        console.log('Worker received TestSharedArrayBuffer; SAB-backed:', int32View.buffer instanceof SharedArrayBuffer, '— calling JSExport');
        self.workerInterop.TestSharedArrayBuffer();
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