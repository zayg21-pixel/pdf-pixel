using Silk.NET.Core.Contexts;
using Silk.NET.Windowing;
using SkiaSharp;

namespace PdfPixel.Console.Demo;

internal static class SkSurfaceFactory
{
    private static GRContext? _grContext;

    public static SKSurface GetSkiaSurface(SKImageInfo imageInfo, bool gpuMode)
    {
        if (!gpuMode)
        {
            return SKSurface.Create(imageInfo);
        }

        _grContext ??= CreateGlGrContext();
        return SKSurface.Create(_grContext, budgeted: true, imageInfo);
    }

    private static GRContext CreateGlGrContext()
    {
        // Compatibility profile, not Core, to match the legacy wglCreateContext used by
        // PdfPixel.PdfPanel.Wpf's WglContext, where GPU rendering is known to work correctly.
        WindowOptions windowOptions = WindowOptions.Default with
        {
            IsVisible = false,
            Size = new Silk.NET.Maths.Vector2D<int>(1, 1),
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(4, 6))
        };

        IWindow window = Window.Create(windowOptions);
        window.Initialize();

        IGLContext glContext = window.GLContext ?? throw new InvalidOperationException("Failed to create an OpenGL context.");
        glContext.MakeCurrent();

        // Skia's own native proc-address loader, same as PdfPixel.PdfPanel.Wpf's OpenGlRenderTargetFactory
        // uses, rather than a hand-written one — it correctly falls back to GetProcAddress for legacy
        // functions that wglGetProcAddress does not resolve.
        GRGlInterface glInterface = GRGlInterface.Create() ?? throw new InvalidOperationException("Failed to create the Skia OpenGL interface.");

        return GRContext.CreateGl(glInterface);
    }
}
