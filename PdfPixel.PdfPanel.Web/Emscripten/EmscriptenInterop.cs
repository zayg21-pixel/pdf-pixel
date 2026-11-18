using System.Runtime.InteropServices;

namespace PdfPixel.PdfPanel.Web.Emscripten;

internal static class EmscriptenInterop
{
    // Creates the canvas of the given id, width by height pixels, in the canvas store the JS view draws from.
    [DllImport("emscripten", EntryPoint = "dotnet_canvas_create")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void CreateCanvas(int id, int width, int height);

    // Puts width*height RGBA pixels on the canvas of the given id; pixels must point to them in the WebAssembly
    // linear heap, pinned for the call, and are read in place.
    [DllImport("emscripten", EntryPoint = "dotnet_canvas_write_rgba")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void WriteCanvasRgba(int id, nint pixels, int width, int height);

    // Removes the canvas of the given id from the canvas store and releases its pixels.
    [DllImport("emscripten", EntryPoint = "dotnet_canvas_delete")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void DeleteCanvas(int id);
}
