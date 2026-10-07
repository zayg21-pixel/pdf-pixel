using System;
using System.Runtime.InteropServices;
using static PdfPixel.PdfPanel.Wpf.OpenGl.WglInterop;

namespace PdfPixel.PdfPanel.Wpf.OpenGl;

/// <summary>
/// Creates and manages an offscreen WGL OpenGL context for GPU rendering.
/// The context is backed by a hidden 1×1 window and is not tied to any visible surface.
/// </summary>
internal sealed class WglContext : IDisposable
{
    private nint _hWnd;
    private nint _hDC;
    private nint _hGLRC;
    private bool _disposed;

    private WglContext()
    {
    }

    ~WglContext() => Dispose(false);

    /// <summary>
    /// Creates a hidden window, sets up a pixel format, and creates a WGL rendering context.
    /// The context is made current on the calling thread before returning.
    /// </summary>
    /// <returns>A fully initialized <see cref="WglContext"/>.</returns>
    public static WglContext Create()
    {
        WglContext context = new();
        context.Initialize();
        return context;
    }

    /// <summary>
    /// Makes this OpenGL context current on the calling thread.
    /// </summary>
    public void MakeCurrent()
    {
        if (!wglMakeCurrent(_hDC, _hGLRC))
        {
            throw new InvalidOperationException("wglMakeCurrent failed.");
        }
    }

    /// <summary>
    /// Releases the OpenGL context from the calling thread.
    /// </summary>
    public void ReleaseCurrent() => wglMakeCurrent(0, 0);

    private void Initialize()
    {
        nint moduleHandle = GetModuleHandleW(null);

        _hWnd = CreateWindowExW(
            0,
            "Static",
            "PdfPixelGlContext",
            WsPopup,
            0,
            0,
            1,
            1,
            0,
            0,
            moduleHandle,
            0);

        if (_hWnd == 0)
        {
            throw new InvalidOperationException("Failed to create hidden window for OpenGL context.");
        }

        _hDC = GetDC(_hWnd);

        if (_hDC == 0)
        {
            DestroyWindow(_hWnd);
            throw new InvalidOperationException("Failed to get device context for OpenGL window.");
        }

        PixelFormatDescriptor pfd = new()
        {
            Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
            Version = 1,
            Flags = PfdDrawToWindow | PfdSupportOpengl | PfdDoubleBuffer,
            PixelType = PfdTypeRgba,
            ColorBits = 32,
            AlphaBits = 0,
            DepthBits = 24,
            StencilBits = 8,
            LayerType = PfdMainPlane
        };

        int pixelFormat = ChoosePixelFormat(_hDC, ref pfd);

        if (pixelFormat == 0)
        {
            _ = ReleaseDC(_hWnd, _hDC);
            DestroyWindow(_hWnd);
            throw new InvalidOperationException("ChoosePixelFormat failed.");
        }

        if (!SetPixelFormat(_hDC, pixelFormat, ref pfd))
        {
            _ = ReleaseDC(_hWnd, _hDC);
            DestroyWindow(_hWnd);
            throw new InvalidOperationException("SetPixelFormat failed.");
        }

        _hGLRC = wglCreateContext(_hDC);

        if (_hGLRC == 0)
        {
            _ = ReleaseDC(_hWnd, _hDC);
            DestroyWindow(_hWnd);
            throw new InvalidOperationException("wglCreateContext failed.");
        }

        MakeCurrent();
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hGLRC != 0)
        {
            wglMakeCurrent(0, 0);
            wglDeleteContext(_hGLRC);
            _hGLRC = 0;
        }

        if (_hDC != 0 && _hWnd != 0)
        {
            _ = ReleaseDC(_hWnd, _hDC);
            _hDC = 0;
        }

        if (_hWnd != 0)
        {
            DestroyWindow(_hWnd);
            _hWnd = 0;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
