#include "emscripten.h"
#include <emscripten/console.h>
#include <stdint.h>

// Creates the canvas of the given id, width by height pixels, in the panel's canvas store, which the JS view reads
// tiles and thumbnails from (globalThis.pdfPixelCanvases).
EM_JS(void, dotnet_canvas_create_js, (int id, int width, int height), {
	const canvases = globalThis.pdfPixelCanvases || (globalThis.pdfPixelCanvases = new Map());
	const canvas = document.createElement('canvas');
	canvas.width = width;
	canvas.height = height;
	canvases.set(id, canvas);
});

// Puts width*height RGBA pixels, read in place from the heap at pixelsPtr, on the canvas of the given id.
EM_JS(void, dotnet_canvas_write_rgba_js, (int id, const uint8_t* pixelsPtr, int width, int height), {
	const canvas = globalThis.pdfPixelCanvases?.get(id);

	if (!canvas) {
		return;
	}

	const context = canvas.pdfPixelContext || (canvas.pdfPixelContext = canvas.getContext('2d'));
	const pixels = new Uint8ClampedArray(HEAPU8.buffer, pixelsPtr, width * height * 4);
	context.putImageData(new ImageData(pixels, width, height), 0, 0);
});

// Removes the canvas of the given id from the canvas store and releases its pixels.
EM_JS(void, dotnet_canvas_delete_js, (int id), {
	const canvases = globalThis.pdfPixelCanvases;
	const canvas = canvases?.get(id);

	if (!canvas) {
		return;
	}

	canvases.delete(id);
	canvas.width = 0;
	canvas.height = 0;
});

void dotnet_canvas_create(int id, int width, int height) {
	dotnet_canvas_create_js(id, width, height);
}

void dotnet_canvas_write_rgba(int id, const uint8_t* pixels, int width, int height) {
	dotnet_canvas_write_rgba_js(id, pixels, width, height);
}

void dotnet_canvas_delete(int id) {
	dotnet_canvas_delete_js(id);
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
