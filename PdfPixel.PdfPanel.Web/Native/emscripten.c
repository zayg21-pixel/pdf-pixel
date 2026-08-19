#include "emscripten.h"
#include <emscripten/html5.h>
#include <emscripten/console.h>
#include <stdint.h>

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

	const canvas = document.querySelector(canvasId);

	if (!canvas) {
		return;
	}

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

// Sets the canvas size and its CSS size, scaled by the device pixel ratio.
void dotnet_set_canvas_size(const char* canvasId, int width, int height) {
	emscripten_set_canvas_element_size(canvasId, width, height);

	double dpr = emscripten_get_device_pixel_ratio();
	emscripten_set_element_css_size(canvasId, width / dpr, height / dpr);
}
