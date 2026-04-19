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
    private IntPtr _hWnd;
    private IntPtr _hDC;
    private IntPtr _hGLRC;
    private bool _disposed;

    private WglContext() { }

    /// <summary>
    /// Creates a hidden window, sets up a pixel format, and creates a WGL rendering context.
    /// The context is made current on the calling thread before returning.
    /// </summary>
    /// <returns>A fully initialized <see cref="WglContext"/>.</returns>
    public static WglContext Create()
    {
        var context = new WglContext();
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
    public void ReleaseCurrent()
    {
        wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
    }

    private void Initialize()
    {
        var moduleHandle = GetModuleHandleW(null);

        _hWnd = CreateWindowExW(
            0,
            "Static",
            "PdfPixelGlContext",
            WsPopup,
            0, 0, 1, 1,
            IntPtr.Zero, IntPtr.Zero, moduleHandle, IntPtr.Zero);

        if (_hWnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create hidden window for OpenGL context.");
        }

        _hDC = GetDC(_hWnd);

        if (_hDC == IntPtr.Zero)
        {
            DestroyWindow(_hWnd);
            throw new InvalidOperationException("Failed to get device context for OpenGL window.");
        }

        var pfd = new PixelFormatDescriptor
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
            ReleaseDC(_hWnd, _hDC);
            DestroyWindow(_hWnd);
            throw new InvalidOperationException("ChoosePixelFormat failed.");
        }

        if (!SetPixelFormat(_hDC, pixelFormat, ref pfd))
        {
            ReleaseDC(_hWnd, _hDC);
            DestroyWindow(_hWnd);
            throw new InvalidOperationException("SetPixelFormat failed.");
        }

        _hGLRC = wglCreateContext(_hDC);

        if (_hGLRC == IntPtr.Zero)
        {
            ReleaseDC(_hWnd, _hDC);
            DestroyWindow(_hWnd);
            throw new InvalidOperationException("wglCreateContext failed.");
        }

        MakeCurrent();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hGLRC != IntPtr.Zero)
        {
            wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            wglDeleteContext(_hGLRC);
            _hGLRC = IntPtr.Zero;
        }

        if (_hDC != IntPtr.Zero && _hWnd != IntPtr.Zero)
        {
            ReleaseDC(_hWnd, _hDC);
            _hDC = IntPtr.Zero;
        }

        if (_hWnd != IntPtr.Zero)
        {
            DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
    }
}
