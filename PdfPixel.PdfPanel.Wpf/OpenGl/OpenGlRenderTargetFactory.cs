using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Wpf.Drawing;
using SkiaSharp;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf.OpenGl;

/// <summary>
/// Experimental GPU-accelerated render target factory that uses OpenGL (WGL) for drawing
/// and reads back pixels to a <see cref="WriteableBitmap"/> for WPF presentation.
/// <para>
/// Flow: render thread draws on GPU-backed <see cref="SKSurface"/> → <c>ReadPixels</c> into
/// a CPU buffer → UI thread copies into <see cref="WriteableBitmap"/> → <see cref="DrawingVisual"/>.
/// </para>
/// </summary>
public sealed class OpenGlRenderTargetFactory : IPdfPanelRenderTargetFactory, IPdfPanelRenderTarget, ISkSurfaceFactory, IDisposable
{
    /// <summary>
    /// Maximum GPU resource cache size in bytes. Limits stencil/texture memory growth
    /// from complex path operations (clipping, masking). On integrated GPUs this memory
    /// comes from shared system RAM and shows as process memory growth.
    /// </summary>
    private const long ResourceCacheLimitBytes = 128_000_000;

    private readonly WpfPdfPanel _panel;
    private readonly int _sampleCount;
    private WglContext? _glContext;
    private GRContext? _grContext;

    // Drawing surface state
    private SKSurface? _currentSurface;
    private int _currentWidth;
    private int _currentHeight;

    // Tiling surface state
    private SKSurface? _tilingSurface;
    private int _tilingWidth;
    private int _tilingHeight;

    // Presentation state (created/updated on UI thread via GetRenderTarget)
    private WriteableBitmap? _writeableBitmap;
    private PdfSize _lastPanelSize;
    private PdfPoint _lastHostScale;
    private PdfPoint _lastHostOffset;

    /// <summary>
    /// Initializes a new <see cref="OpenGlRenderTargetFactory"/> for the specified panel.
    /// The actual OpenGL context is created later in <see cref="Initialize"/> on the render thread.
    /// </summary>
    /// <param name="panel">The WPF panel that owns the <see cref="DrawingVisual"/>.</param>
    /// <param name="sampleCount">Number of samples for multisampling.</param>
    public OpenGlRenderTargetFactory(WpfPdfPanel panel, int sampleCount = 1)
    {
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        _sampleCount = sampleCount;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates the offscreen WGL context and SkiaSharp <see cref="GRContext"/> on the render thread.
    /// </remarks>
    public void Initialize()
    {
        if (_glContext != null)
        {
            return;
        }

        _glContext = WglContext.Create();

        using GRGlInterface glInterface = GRGlInterface.Create();

        if (glInterface == null)
        {
            throw new InvalidOperationException("Failed to create GRGlInterface for WGL context.");
        }

        // StencilBuffers causes most of blending issues. Though, disabling them does not solve ALL issues
        _grContext = GRContext.CreateGl(glInterface, new GRContextOptions { RuntimeProgramCacheSize = 128 });
        _grContext.SetResourceCacheLimit(ResourceCacheLimitBytes);

        if (_grContext == null)
        {
            throw new InvalidOperationException("Failed to create GRContext for WGL context.");
        }
    }

    /// <inheritdoc />
    public SKSurface GetDrawingSurface(int width, int height)
    {
        if (_glContext == null || _grContext == null)
        {
            throw new InvalidOperationException("The OpenGL context is not initialized.");
        }

        _glContext.MakeCurrent();

        if (_currentSurface != null && _currentWidth == width && _currentHeight == height)
        {
            return _currentSurface;
        }

        SKImageInfo info = new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        SKSurface newSurface = SKSurface.Create(_grContext, budgeted: true, info, sampleCount: _sampleCount);

        if (newSurface == null)
        {
            throw new InvalidOperationException($"Failed to create OpenGL-backed SKSurface ({width}x{height}).");
        }

        newSurface.Canvas.ClipRect(new SKRect(0, 0, width, height));

        if (_currentSurface != null)
        {
            newSurface.Canvas.DrawSurface(_currentSurface, SKPoint.Empty);
        }

        SKSurface? oldSurface = _currentSurface;
        _currentSurface = newSurface;
        _currentWidth = width;
        _currentHeight = height;

        oldSurface?.Dispose();

        return _currentSurface;
    }

    /// <inheritdoc />
    public PdfMatrix HostToPanel { get; private set; } = PdfMatrix.Identity;

    /// <inheritdoc />
    /// <remarks>
    /// Called from the UI thread. Creates or reuses a <see cref="WriteableBitmap"/> that
    /// matches the current panel dimensions.
    /// </remarks>
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
    {
        PdfSize panelSize = new((float)_panel.PanelSize.Width, (float)_panel.PanelSize.Height);
        PdfPoint hostScale = new((float)_panel.HostScale.X, (float)_panel.HostScale.Y);
        PdfPoint hostOffset = new((float)_panel.HostOffset.X, (float)_panel.HostOffset.Y);

        if (_writeableBitmap == null
            || _lastPanelSize != panelSize
            || _lastHostScale != hostScale
            || _lastHostOffset != hostOffset)
        {
            _writeableBitmap = new WriteableBitmap(
                (int)panelSize.Width,
                (int)panelSize.Height,
                96.0 * hostScale.X,
                96.0 * hostScale.Y,
                PixelFormats.Pbgra32,
                null);

            _lastPanelSize = panelSize;
            _lastHostScale = hostScale;
            _lastHostOffset = hostOffset;
            HostToPanel = _panel.GetHostToPanelMatrix(hostOffset, hostScale);
        }

        return this;
    }

    /// <inheritdoc />
    public void Render(SKSurface surface, PdfPanelFrame frame)
    {
        if (surface == null)
        {
            throw new ArgumentNullException(nameof(surface));
        }

        if (_glContext == null || _grContext == null)
        {
            throw new InvalidOperationException("The OpenGL context is not initialized.");
        }

        SKImageInfo imageInfo = new(_currentWidth, _currentHeight, SKColorType.Bgra8888, SKAlphaType.Premul);

        _glContext.MakeCurrent();
        _grContext.Flush();

        PresentToWriteableBitmap(imageInfo, surface, frame);
    }

    private void PresentToWriteableBitmap(SKImageInfo imageInfo, SKSurface surface, PdfPanelFrame frame)
    {
        if (_writeableBitmap == null || _panel.DrawingVisual == null)
        {
            return;
        }

        if (_writeableBitmap.PixelWidth != imageInfo.Width || _writeableBitmap.PixelHeight != imageInfo.Height)
        {
            return;
        }

        _writeableBitmap.Lock();

        surface.ReadPixels(imageInfo, _writeableBitmap.BackBuffer, imageInfo.RowBytes, 0, 0);

        if (_panel.PanelInterface?.OnAfterDraw != null)
        {
            using SKSurface drawSurface = SKSurface.Create(imageInfo, _writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride);
            _panel.PanelInterface.OnAfterDraw(drawSurface.Canvas, frame);
        }

        _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, imageInfo.Width, imageInfo.Height));

        DrawingVisual drawingVisual = _panel.DrawingVisual;
        DrawingContext render = drawingVisual.RenderOpen();

        double pixelOffsetX = _panel.SnapPosition(_lastHostOffset.X, _lastHostScale.X);
        double pixelOffsetY = _panel.SnapPosition(_lastHostOffset.Y, _lastHostScale.Y);

        render.DrawImage(_writeableBitmap, new Rect(pixelOffsetX, pixelOffsetY, _writeableBitmap.Width, _writeableBitmap.Height));

        render.Close();

        _writeableBitmap.Unlock();
    }

    /// <inheritdoc />
    public SKSurface GetTilingSurface(int width, int height)
    {
        if (_glContext == null || _grContext == null)
        {
            throw new InvalidOperationException("The OpenGL context is not initialized.");
        }

        _glContext.MakeCurrent();

        if (_tilingSurface != null && _tilingWidth == width && _tilingHeight == height)
        {
            return _tilingSurface;
        }

        _tilingSurface?.Dispose();
        SKImageInfo info = new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _tilingSurface = SKSurface.Create(_grContext, budgeted: true, info);
        _tilingWidth = width;
        _tilingHeight = height;

        return _tilingSurface;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _currentSurface?.Dispose();
        _currentSurface = null;

        _tilingSurface?.Dispose();
        _tilingSurface = null;

        _grContext?.Dispose();
        _grContext = null;

        _glContext?.Dispose();
        _glContext = null;
    }
}
