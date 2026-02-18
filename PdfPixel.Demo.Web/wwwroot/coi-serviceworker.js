/* coi-serviceworker - Cross-Origin Isolation via Service Worker
 * Based on https://github.com/gzuidhof/coi-serviceworker (MIT License)
 *
 * Required because GitHub Pages is a static host that cannot set HTTP response
 * headers. This service worker intercepts every fetch and injects the
 * Cross-Origin-Opener-Policy and Cross-Origin-Embedder-Policy headers so that
 * SharedArrayBuffer (needed by Blazor WASM threading) is available.
 *
 * When running locally, Program.cs already sets these headers via middleware,
 * so the service worker skips registration in that case (window.crossOriginIsolated === true).
 */
if (typeof window === "undefined") {
    // ---- Running as a Service Worker ----

    self.addEventListener("install", () => self.skipWaiting());

    self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));

    async function handleFetch(request) {
        // Avoid a known Chrome bug with cache-only same-origin requests.
        if (request.cache === "only-if-cached" && request.mode !== "same-origin") {
            return;
        }

        const response = await fetch(request);

        // Opaque responses (status 0) cannot be modified.
        if (response.status === 0) {
            return response;
        }

        const newHeaders = new Headers(response.headers);
        newHeaders.set("Cross-Origin-Opener-Policy", "same-origin");
        newHeaders.set("Cross-Origin-Embedder-Policy", "require-corp");
        // Allow cross-origin subresources (fonts, WASM, etc.) to be loaded.
        newHeaders.set("Cross-Origin-Resource-Policy", "cross-origin");

        return new Response(response.body, {
            status: response.status,
            statusText: response.statusText,
            headers: newHeaders,
        });
    }

    self.addEventListener("fetch", (event) => event.respondWith(handleFetch(event.request)));
} else {
    // ---- Running as a regular page script ----
    (async function () {
        // Already isolated (e.g. local dev with ASP.NET Core middleware) — nothing to do.
        if (window.crossOriginIsolated !== false) {
            return;
        }

        if (!navigator.serviceWorker) {
            console.error(
                "SharedArrayBuffer is not available and Service Workers are not supported. " +
                "Please use a modern browser."
            );
            return;
        }

        try {
            await navigator.serviceWorker.register(window.document.currentScript.src);
            await navigator.serviceWorker.ready;
        } catch (error) {
            console.error("coi-serviceworker: registration failed.", error);
            return;
        }

        // The service worker is registered but not yet controlling this page.
        // Reload so it takes effect immediately.
        if (!navigator.serviceWorker.controller) {
            window.location.reload();
        }
    })();
}
