using System;
using WebGL.Sample;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Encapsulates the shared EGL display and configuration, initialized once per application.
/// All EGL calls may be made from any thread; Emscripten proxies them to the browser main thread transparently.
/// </summary>
public sealed class EglGlobalContext : IDisposable
{
    private bool _disposed;

    /// <summary>Gets the EGL display handle.</summary>
    internal IntPtr Display { get; }

    /// <summary>Gets the chosen EGL configuration handle.</summary>
    internal IntPtr Config { get; }

    /// <summary>Gets the actual MSAA sample count reported by the EGL configuration.</summary>
    public int Samples { get; }

    /// <summary>Gets the stencil buffer bit depth reported by the EGL configuration.</summary>
    public int StencilBits { get; }

    private EglGlobalContext(IntPtr display, IntPtr config, int samples, int stencilBits)
    {
        Display = display;
        Config = config;
        Samples = samples;
        StencilBits = stencilBits;
    }

    /// <summary>
    /// Initializes the EGL display, selects a configuration, and returns the shared global context.
    /// May be called from any thread.
    /// </summary>
    /// <returns>A fully initialized <see cref="EglGlobalContext"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if any EGL initialization step fails.</exception>
    public static EglGlobalContext Initialize()
    {
        var display = EGL.GetDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
            throw new InvalidOperationException("EGL.GetDisplay() returned null.");

        if (!EGL.Initialize(display, out int major, out int minor))
            throw new InvalidOperationException("EGL.Initialize() failed.");

        Console.WriteLine($"EGL initialized: version {major}.{minor}");

        int[] attributeList =
        [
            EGL.EGL_RED_SIZE,        8,
            EGL.EGL_GREEN_SIZE,      8,
            EGL.EGL_BLUE_SIZE,       8,
            EGL.EGL_DEPTH_SIZE,      24,
            EGL.EGL_STENCIL_SIZE,    8,
            EGL.EGL_SURFACE_TYPE,    EGL.EGL_WINDOW_BIT,
            EGL.EGL_RENDERABLE_TYPE, EGL.EGL_OPENGL_ES3_BIT,
            EGL.EGL_NONE
        ];

        var config = IntPtr.Zero;
        var numConfig = IntPtr.Zero;
        if (!EGL.ChooseConfig(display, attributeList, ref config, (IntPtr)1, ref numConfig))
            throw new InvalidOperationException("EGL.ChooseConfig() failed.");

        if (numConfig == IntPtr.Zero)
            throw new InvalidOperationException("EGL.ChooseConfig() returned no matching configurations.");

        if (!EGL.BindApi(EGL.EGL_OPENGL_ES_API))
            throw new InvalidOperationException("EGL.BindApi() failed.");

        int samples = QueryConfigAttrib(display, config, EGL.EGL_SAMPLES);
        int stencilBits = QueryConfigAttrib(display, config, EGL.EGL_STENCIL_SIZE);
        Console.WriteLine($"EGL config: {samples} MSAA samples, {stencilBits} stencil bits");

        return new EglGlobalContext(display, config, samples, stencilBits);
    }

    /// <summary>Queries a single integer attribute from an EGL configuration.</summary>
    private static int QueryConfigAttrib(IntPtr display, IntPtr config, int attribute)
    {
        var valuePtr = IntPtr.Zero;
        if (EGL.GetConfigAttrib(display, config, (IntPtr)attribute, ref valuePtr))
        {
            return (int)valuePtr;
        }

        return 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ = EGL.Terminate(Display);
    }
}
