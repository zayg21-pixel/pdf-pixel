using SkiaSharp;
using System;
using System.Threading.Tasks;
using WebGL.Sample;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Encapsulates per-canvas EGL handles and the Skia GPU context.
/// EGL calls may be made from any thread; Emscripten proxies them transparently.
/// Skia operations (<see cref="GRContext"/>, <see cref="SKSurface"/>) require the browser main thread
/// and are marshalled accordingly.
/// </summary>
/// <remarks>
/// Before calling <see cref="CreateAsync"/>, ensure <c>Module["canvas"]</c> is set to the target
/// canvas element so that <c>eglCreateWindowSurface</c> binds to the correct WebGL context.
/// </remarks>
public sealed class CanvasGlContext : IDisposable
{
    private bool _disposed;

    private CanvasGlContext(
    string canvasSelector,
    int webGlContext,
    GRContext grContext)
    {
        CanvasSelector = canvasSelector;
        WebGlContext = webGlContext;
        GrContext = grContext;
    }

    public string CanvasSelector { get; }

    public int WebGlContext { get; }

    /// <summary>Gets the Skia GPU context for this canvas.</summary>
    public GRContext GrContext { get; }

    public static async Task<CanvasGlContext> CreateAsync(string canvasSelector)
    {
        CanvasGlContext context = null;
        await Emscripten.RunOnMainThreadAsync(() =>
        {
            var webglCtx = Emscripten.WebGlCreateContext(
                canvasId: canvasSelector, alpha: 1, depth: 1, stencil: 1, antialias: 1, majorVersion: 2);
            if (webglCtx <= 0)
                throw new Exception($"emscripten_webgl_create_context failed for {canvasSelector}: {webglCtx}");

            var result = Emscripten.WebGlMakeContextCurrent(webglCtx);
            if (result != 0)
                throw new Exception($"emscripten_webgl_make_context_current failed for {canvasSelector}: {result}");

            Console.WriteLine($"WebGL context handle for {canvasSelector}: {webglCtx}");

            using var glInterface = GRGlInterface.Create();
            if (glInterface == null)
            {
                Console.WriteLine("Failed to create GRGlInterface");
            }

            var grContext = GRContext.CreateGl(glInterface);
            Console.WriteLine("Done trying hard");
            Console.WriteLine(grContext == null
                ? "Failed to create GRContext"
                : "SkiaSharp GRContext created successfully!");

            context = new CanvasGlContext(canvasSelector, webglCtx, grContext);
        });

        return context;
    }

    /// <summary>
    /// Creates an <see cref="SKSurface"/> targeting framebuffer 0 of this canvas.
    /// Must be called on the browser main thread.
    /// </summary>
    /// <param name="width">The surface width in pixels.</param>
    /// <param name="height">The surface height in pixels.</param>
    /// <param name="oldSurface">Old surface that existed before to be disposed.</param>
    /// <returns>A new <see cref="SKSurface"/> backed by this canvas's WebGL framebuffer.</returns>
    public async Task<SKSurface> CreateSurfaceAsync(int width, int height, SKSurface oldSurface = null)
    {
        SKSurface surface = null;
        await Emscripten.RunOnMainThreadAsync(() =>
        {
            Emscripten.WebGlMakeContextCurrent(WebGlContext);

            // Read back the old surface content into CPU memory BEFORE resizing the canvas.
            // SetCanvasSize invalidates/clears the WebGL framebuffer, so a GPU snapshot
            // taken after that point would be empty. We must flush and copy pixels now.
            SKImage cpuSnapshot = null;
            if (oldSurface != null)
            {
                oldSurface.Flush();
                using var gpuSnapshot = oldSurface.Snapshot();
                cpuSnapshot = gpuSnapshot?.ToRasterImage();
            }

            Emscripten.SetCanvasSize(CanvasSelector, width, height);

            var glInfo = new GRGlFramebufferInfo(
                fboId: 1, // still have no idea why this is 1, but it works ¯\_(ツ)_/¯
                format: 0x8058); // GL_RGBA8

            var renderTarget = new GRBackendRenderTarget(
                width,
                height,
                sampleCount: 0,
                stencilBits: 8,
                glInfo);

            surface = SKSurface.Create(
                GrContext,
                renderTarget,
                GRSurfaceOrigin.BottomLeft,
                SKColorType.Rgba8888);

            if (cpuSnapshot != null)
            {
                surface.Canvas.DrawImage(cpuSnapshot, new SKPoint(0, 0));
                surface.Flush();
                cpuSnapshot.Dispose();
            }

            oldSurface?.Dispose();
        });

        return surface;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Disposes the Skia GPU context and releases EGL handles.
    /// Should be called from the browser main thread so that <see cref="GRContext"/> can
    /// flush and release GPU resources while the context is current.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GrContext.Dispose();
        // TODO: destroy WebGlContext!!!
    }
}
