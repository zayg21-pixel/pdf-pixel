using System.Runtime.InteropServices;

namespace PdfPixel.PdfPanel.Web.Emscripten;

internal static class EmscriptenInterop
{
    // Creates a WebGL context on the specified canvas with the given attributes.
    // renderViaOffscreenBackBuffer=1 is set internally — required for OFFSCREEN_FRAMEBUFFER
    // worker rendering. Returns the handle (> 0) on success, negative EMSCRIPTEN_RESULT on error.
    [DllImport("emscripten", EntryPoint = "dotnet_webgl_create_context")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int WebGlCreateContext(string canvasId, int alpha, int depth, int stencil, int antialias, int majorVersion);

    // Makes the given WebGL context current on the calling thread.
    [DllImport("emscripten", EntryPoint = "dotnet_webgl_make_context_current")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int WebGlMakeContextCurrent(int context);

    // Sets the canvas size. Must be called from the main browser thread.
    // Setting canvas dimensions clears the canvas, so call this just before recreating the render target.
    [DllImport("emscripten", EntryPoint = "dotnet_set_canvas_size")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void SetCanvasSize(string canvasId, int width, int height);

    // Sets an RGBA image (bytes) to a canvas identified by selector (e.g. "#myCanvas").
    // pixels must point to width*height*4 bytes in RGBA order. The native side expects
    // the pixel buffer to be in the WebAssembly linear heap; use the ReadOnlySpan wrapper
    // below to pin managed memory and pass a pointer.
    [DllImport("emscripten", EntryPoint = "dotnet_set_canvas_rgba")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void SetCanvasRgba(string canvasId, nint pixels, int width, int height);

    // Starts the render loop. The callback is called once per frame by the browser.
    // fps: target frames per second. Use 0 to use requestAnimationFrame timing (recommended).
    // simulateInfiniteLoop: if 1, this function never returns. If 0, returns immediately.
    [DllImport("emscripten", EntryPoint = "dotnet_start_main_loop")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern unsafe void StartMainLoop(delegate* unmanaged<void> callback, int fps, int simulateInfiniteLoop);

    // Stops the render loop.
    [DllImport("emscripten", EntryPoint = "dotnet_stop_main_loop")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void StopMainLoop();

    // Pauses the render loop without canceling it.
    [DllImport("emscripten", EntryPoint = "dotnet_pause_main_loop")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void PauseMainLoop();

    // Resumes a paused render loop.
    [DllImport("emscripten", EntryPoint = "dotnet_resume_main_loop")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void ResumeMainLoop();

    // Returns Atomics.load(self.testSharedView, 0). Used by the SAB design test;
    // self.testSharedView is set on the worker by pdfContentWorker.js before
    // calling the spinning JSExport.
    [DllImport("emscripten", EntryPoint = "dotnet_test_read_shared_byte")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int TestReadSharedByte();
}
