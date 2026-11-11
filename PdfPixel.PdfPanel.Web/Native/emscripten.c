#include "emscripten.h"
#include <emscripten/html5.h>
#include <emscripten/console.h>
#include <stdint.h>

// Puts an RGBA image (bytes) on a canvas identified by selector (e.g. "#myCanvas").
// pixelsPtr must point to width*height*4 bytes in RGBA order; they are read in place from the heap, and the 2D
// context of each canvas is kept while the canvas stays in the document.
EM_JS(void, dotnet_set_canvas_rgba_js, (const char* canvasIdPtr, const uint8_t* pixelsPtr, int width, int height), {
	const canvasId = UTF8ToString(canvasIdPtr);
	const contexts = Module.pdfPixelCanvasContexts || (Module.pdfPixelCanvasContexts = new Map());
	let ctx = contexts.get(canvasId);

	if (!ctx || !ctx.canvas.isConnected) {
		const canvas = document.querySelector(canvasId);

		if (!canvas) {
			contexts.delete(canvasId);
			return;
		}

		ctx = canvas.getContext('2d');

		if (!ctx) {
			contexts.delete(canvasId);
			return;
		}

		contexts.set(canvasId, ctx);
	}

	const pixels = new Uint8ClampedArray(HEAPU8.buffer, pixelsPtr, width * height * 4);
	ctx.putImageData(new ImageData(pixels, width, height), 0, 0);
});

// C wrapper for the EM_JS function. Call this from C/C# with a pointer to RGBA bytes.
void dotnet_set_canvas_rgba(const char* canvasId, const uint8_t* pixels, int width, int height) {
	dotnet_set_canvas_rgba_js(canvasId, pixels, width, height);
}

// Writes a log message to the browser console using the channel that matches log_level.
// log_level mirrors Microsoft.Extensions.Logging.LogLevel:
//   0=Trace, 1=Debug, 2=Information  → console.log
//   3=Warning                        → console.warn
//   4=Error, 5=Critical              → console.error
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
