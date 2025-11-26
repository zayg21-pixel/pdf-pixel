// renderWorker.js
// This worker receives { pageIndex } and returns { pageIndex, skpData }
let sharpInterop = null;
console.log('Created!');

self.onmessage = async function (e) {
    const { type, pageIndex } = e.data;
    if (type === 'init') {
        // Import main.js and initialize sharpInterop
        importScripts('main.js');
        if (typeof GetInterop === 'function') {
            sharpInterop = await GetInterop();
            self.postMessage({ type: 'init', success: true });
        } else {
            self.postMessage({ type: 'init', success: false });
        }
        return;
    }
    if (type === 'render' && sharpInterop) {
        // Not really async, so no await
        const skpData = sharpInterop.RenderPageToSkp(pageIndex);
        // Transferable objects: use skpData.buffer if possible
        self.postMessage({ type: 'render', pageIndex, skpData }, [skpData.buffer]);
    }
};
