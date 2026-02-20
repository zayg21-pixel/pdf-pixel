#include "emscripten.h"
#include <emscripten/threading.h>

// Run func() on the browser main thread and block until it returns.
// Safe to call from any thread, including the main thread itself.
void dotnet_sync_main_thread(void (*func)(void)) {
    if (emscripten_is_main_browser_thread()) {
        func();
    } else {
        emscripten_sync_run_in_main_runtime_thread(EM_FUNC_SIG_V, func);
    }
}

// Pointer-sized integer type and matching EM_FUNC_SIG for emscripten threading:
//   MEMORY64=0 (wasm32): void* == int32_t  → EM_FUNC_SIG_VI
//   MEMORY64=1 (wasm64): void* == int64_t  → EM_FUNC_SIG_VJ
//   MEMORY64=2 (wasm64 lowered to wasm32 by Binaryen): not clearly defined yet.
#ifdef __wasm64__
#  define EM_FUNC_SIG_VPTR EM_FUNC_SIG_VJ
   typedef int64_t em_ptr_int_t;
#else
#  define EM_FUNC_SIG_VPTR EM_FUNC_SIG_VI
   typedef int32_t em_ptr_int_t;
#endif

// Same as dotnet_sync_main_thread, but forwards a single pointer-sized argument.
// On the C# side use nint / delegate* unmanaged<nint, void> — nint matches em_ptr_int_t
// on both wasm32 and wasm64.
void dotnet_sync_main_thread_arg(void (*func)(em_ptr_int_t), em_ptr_int_t arg) {
    if (emscripten_is_main_browser_thread()) {
        func(arg);
    } else {
        emscripten_sync_run_in_main_runtime_thread(EM_FUNC_SIG_VPTR, func, arg);
    }
}

// Non-blocking variant: posts func(arg) to the main thread and returns immediately.
// Used to implement C# async/await dispatch — the worker thread suspends via
// TaskCompletionSource rather than blocking, so neither thread can deadlock on the other.
void dotnet_async_run_in_main_runtime_thread(void (*func)(em_ptr_int_t), em_ptr_int_t arg) {
    if (emscripten_is_main_browser_thread()) {
        func(arg);
    } else {
        emscripten_async_run_in_main_runtime_thread(EM_FUNC_SIG_VPTR, func, arg);
    }
}

// Returns the WebGL context handle that is current on the calling thread (0 if none).
// With OFFSCREEN_FRAMEBUFFER=1 this works correctly from any pthread.
int dotnet_webgl_get_current_context(void) {
    return (int)emscripten_webgl_get_current_context();
}

// Makes the given WebGL context current on the calling thread.
// With OFFSCREEN_FRAMEBUFFER=1, calling this from a pthread sets up the offscreen proxy
// so that all subsequent GL calls are forwarded to the main browser thread.
int dotnet_webgl_make_context_current(int ctx) {
    return (int)emscripten_webgl_make_context_current((EMSCRIPTEN_WEBGL_CONTEXT_HANDLE)ctx);
}

// Creates a WebGL context on the specified canvas.
// renderViaOffscreenBackBuffer=1 is required for OFFSCREEN_FRAMEBUFFER cross-thread rendering:
// it activates the per-context offscreen proxy that forwards worker GL calls to the main thread.
// Returns the handle (> 0) on success, or a negative EMSCRIPTEN_RESULT error code.
int dotnet_webgl_create_context(const char* canvasId, int alpha, int depth, int stencil, int antialias, int majorVersion) {
	EmscriptenWebGLContextAttributes attrs;
	emscripten_webgl_init_context_attributes(&attrs);
	attrs.alpha = alpha;
	attrs.depth = depth;
	attrs.stencil = stencil;
	attrs.antialias = antialias;
	attrs.majorVersion = majorVersion;
	attrs.minorVersion = 0;
	attrs.enableExtensionsByDefault = 1;
	attrs.renderViaOffscreenBackBuffer = 0;
	attrs.explicitSwapControl = 0;
	attrs.preserveDrawingBuffer = 1;
	attrs.proxyContextToMainThread = EMSCRIPTEN_WEBGL_CONTEXT_PROXY_ALWAYS;
	return (int)emscripten_webgl_create_context(canvasId, &attrs);
}

// Sets the canvas size directly. This must be called from the main browser thread.
// Setting canvas.width/height clears the canvas content, so this should be called
// just before recreating the render target to minimize flicker.
EM_JS(void, dotnet_set_canvas_size_js, (const char* canvasId, int width, int height), {
	const canvasIdStr = UTF8ToString(canvasId);
	const canvas = document.querySelector(canvasIdStr);
	if (canvas) {
        const dpr = window.devicePixelRatio || 1;
        const zoom = (window.visualViewport && window.visualViewport.scale) ? window.visualViewport.scale : 1;
        const devicePixelScale = dpr * zoom;
		canvas.width = width;
		canvas.height = height;
        canvas.style.width = (width / devicePixelScale) + 'px';
        canvas.style.height = (height / devicePixelScale) + 'px';
	}
});

void dotnet_set_canvas_size(const char* canvasId, int width, int height) {
	dotnet_set_canvas_size_js(canvasId, width, height);
}
