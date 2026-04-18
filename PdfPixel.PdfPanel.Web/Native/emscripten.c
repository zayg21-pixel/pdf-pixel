#include "emscripten.h"
#include <emscripten/threading.h>
#include <emscripten/console.h>
#include <stdint.h>

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
    if (emscripten_is_main_runtime_thread()) {
        func(arg);
    } else {
        emscripten_sync_run_in_main_runtime_thread(EM_FUNC_SIG_VPTR, func, arg);
    }
}

// Makes the given WebGL context current on the calling thread.
int dotnet_webgl_make_context_current(int ctx) {
	return (int)emscripten_webgl_make_context_current((EMSCRIPTEN_WEBGL_CONTEXT_HANDLE)ctx);
}

// Acquires the WEBGL_debug_renderer_info extension on the given context handle so that
// Emscripten's glGetString shim can call getParameter(UNMASKED_RENDERER/VENDOR_WEBGL)
// without triggering an INVALID_ENUM browser warning.
// Must be called from the same thread that owns the context (worker for OffscreenCanvas).
EM_JS(void, dotnet_webgl_enable_debug_renderer_info, (int ctx), {
	if (typeof GL === 'undefined' || !GL.contexts || !GL.contexts[ctx]) {
		return;
	}
	const glCtx = GL.contexts[ctx].GLctx;
	if (glCtx) {
		glCtx.getExtension('WEBGL_debug_renderer_info');
	}
});

// Sets an RGBA image (bytes) to a canvas identified by selector (e.g. "#myCanvas").
// pixelsPtr must point to width*height*4 bytes in RGBA order.
EM_JS(void, dotnet_set_canvas_rgba_js, (const char* canvasIdPtr, const uint8_t* pixelsPtr, int width, int height), {
	const canvasId = UTF8ToString(canvasIdPtr);
	const key = canvasId.substring(1);

	if (typeof GL === 'undefined' || !GL.offscreenCanvases) {
		return;
	}

	const canvas = GL.offscreenCanvases[key];

	const ctx = canvas.getContext('2d');
	if (!ctx) {
		return;
	}

	const size = width * height * 4;
	const src = HEAPU8.subarray(pixelsPtr, pixelsPtr + size);
	// Create a copy as Uint8ClampedArray required by ImageData
	const clamped = new Uint8ClampedArray(src);
	const imageData = new ImageData(clamped, width, height);
	ctx.putImageData(imageData, 0, 0);
});

// C wrapper for the EM_JS function. Call this from C/C# with a pointer to RGBA bytes.
void dotnet_set_canvas_rgba(const char* canvasId, const uint8_t* pixels, int width, int height) {
	dotnet_set_canvas_rgba_js(canvasId, pixels, width, height);
}

// Creates a WebGL context on the specified canvas.
// The OffscreenCanvas must already be registered in GL.offscreenCanvases
// (transferred via the 'run' command's offscreenCanvases property).
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
	attrs.preserveDrawingBuffer = 1;

	int handle = (int)emscripten_webgl_create_context(canvasId, &attrs);
	if (handle > 0) {
		dotnet_webgl_enable_debug_renderer_info(handle);
	}
	return handle;
}

// Writes a log message to the browser console using the channel that matches log_level.
// log_level mirrors Microsoft.Extensions.Logging.LogLevel:
//   0=Trace, 1=Debug, 2=Information  → console.log
//   3=Warning                        → console.warn
//   4=Error, 5=Critical              → console.error
// Safe to call from any pthread — emscripten_console_* never synchronise with the main thread.
void dotnet_console_log(const char* message, int log_level) {
	if (log_level >= 4) {
		emscripten_console_error(message);
	} else if (log_level == 3) {
		emscripten_console_warn(message);
	} else {
		emscripten_console_log(message);
	}
}

// Sets the OffscreenCanvas size from the worker thread.
// The canvas must be registered in GL.offscreenCanvases (transferred via 'run' command).
// Key is canvasId with leading '#' stripped (substring(1)).
EM_JS(void, dotnet_set_offscreen_canvas_size_js, (const char* canvasIdPtr, int width, int height), {
	const canvasId = UTF8ToString(canvasIdPtr);
	const key = canvasId.substring(1);

	if (typeof GL === 'undefined' || !GL.offscreenCanvases) {
		return;
	}

	const canvas = GL.offscreenCanvases[key];
	if (canvas) {
		canvas.width = width;
		canvas.height = height;
	}
});

// Sets canvas size: OffscreenCanvas on worker, main canvas dispatched to main thread.
// Can be called from any thread.
void dotnet_set_canvas_size(const char* canvasId, int width, int height) {
	dotnet_set_offscreen_canvas_size_js(canvasId, width, height);

	double dpr = emscripten_get_device_pixel_ratio();
	emscripten_set_element_css_size(canvasId, width / dpr, height / dpr);
}

// Render loop callback - stored globally since emscripten_set_main_loop doesn't support userData.
static void (*g_render_callback)(void) = NULL;

static void main_loop_iteration(void) {
	if (g_render_callback != NULL) {
		g_render_callback();
	}
}

// Starts the render loop. The callback is called once per frame by the browser.
// fps: target frames per second. Use 0 to use requestAnimationFrame timing (recommended).
// simulate_infinite_loop: if 1, this function never returns (use for main thread).
//                         if 0, returns immediately and the loop runs asynchronously.
void dotnet_start_main_loop(void (*callback)(void), int fps, int simulate_infinite_loop) {
	g_render_callback = callback;
	emscripten_set_main_loop(main_loop_iteration, fps, simulate_infinite_loop);
}

// Stops the render loop.
void dotnet_stop_main_loop(void) {
	emscripten_cancel_main_loop();
	g_render_callback = NULL;
}

// Pauses the render loop without canceling it.
void dotnet_pause_main_loop(void) {
	emscripten_pause_main_loop();
}

// Resumes a paused render loop.
void dotnet_resume_main_loop(void) {
	emscripten_resume_main_loop();
}