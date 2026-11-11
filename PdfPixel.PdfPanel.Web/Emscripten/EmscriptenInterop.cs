using System.Runtime.InteropServices;

namespace PdfPixel.PdfPanel.Web.Emscripten;

internal static class EmscriptenInterop
{
    // Sets the canvas size.
    // Setting canvas dimensions clears the canvas, so call this just before drawing a frame of the new size.
    [DllImport("emscripten", EntryPoint = "dotnet_set_canvas_size")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void SetCanvasSize(string canvasId, int width, int height);

    // Sets an RGBA image (bytes) to a canvas identified by selector (e.g. "#myCanvas").
    // pixels must point to width*height*4 bytes in RGBA order in the WebAssembly linear heap, pinned for the call;
    // the canvas reads them in place.
    [DllImport("emscripten", EntryPoint = "dotnet_set_canvas_rgba")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void SetCanvasRgba(string canvasId, nint pixels, int width, int height);
}
