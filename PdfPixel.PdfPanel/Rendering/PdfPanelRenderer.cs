using Microsoft.Extensions.Logging;
using PdfPixel.Annotations;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Scheduling;
using PdfPixel.PdfPanel.Thumbnails;
using PdfPixel.Snap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Drives the rendering loop for a PDF panel.
/// On each <see cref="Submit(PagesDrawingRequest)"/> call it renders immediately from the current cache. When decoding
/// completes, individual pages are re-rendered on the UI thread without redrawing the whole panel.
/// </summary>
public sealed partial class PdfPanelRenderer : IDisposable
{
    private readonly PdfPanelDocument _panelDocument;
    private readonly CommandCache _frameCache = new();

    private readonly PdfSnapSurfaceParameters _frameParameters = new(
        PdfDeviceRgbColorSpaceConverter.Instance,
        new PdfSnapMemoryPool(),
        new PdfSnapImageCache(tileSize: 256, putCapacityMegabytes: 0, rentCapacityMegabytes: 0));

    private readonly PdfPanelGraphics _graphics;
    private PdfCommandExecutionContext _frameContext;
    private PdfSnapSurface? _frame;
    private PdfImageSamples? _frameSamples;
    private PageShadow? _shadow;
    private PagesDrawingRequest? _lastRequest;
    private bool _disposed;

    /// <summary>
    /// Initializes the renderer and subscribes to <see cref="PdfPanelDocument.PageUpdated"/>.
    /// </summary>
    internal PdfPanelRenderer(PdfPanelDocument panelDocument, PdfPanelGraphics graphics)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _frameContext = new PdfCommandExecutionContext(
            _frameCache,
            new PdfCommandExecutionParameters(),
            panelDocument.DocumentLocker,
            panelDocument.Document.OptionalContentProperties.GetDefaultStates(),
            PdfAnnotationStates.Empty,
            new PdfCancellationExecutionObserver(CancellationToken.None));

        _panelDocument.PageUpdated += OnPageUpdated;
    }

    /// <summary>
    /// Renders the current cache state immediately.
    /// </summary>
    internal void Submit(PagesDrawingRequest request)
    {
        if (_disposed)
        {
            return;
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.Equals(_lastRequest))
        {
            return;
        }

        RenderAll(request);
        _lastRequest = request;
    }

    /// <summary>
    /// Redraws the visible pages among <paramref name="pageNumbers"/> with their current graphics.
    /// </summary>
    internal void RedrawGraphics(IReadOnlyList<int> pageNumbers)
    {
        if (_disposed || pageNumbers.Count == 0 || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        PdfSnapSurface surface = GetSurface(_lastRequest);
        PdfPanelFrame panelFrame = CreateFrame(_lastRequest, _lastRequest.Pages);
        var pageDrawn = false;

        foreach (RequestPageInfo page in _lastRequest.Pages)
        {
            if (!pageNumbers.Contains(page.PageNumber))
            {
                continue;
            }

            DrawPageContent(surface, page, _lastRequest, panelFrame);
            pageDrawn = true;
        }

        if (pageDrawn)
        {
            Present(_lastRequest, panelFrame);
        }
    }

    /// <summary>
    /// Fills the surface with the background and presents it. Called when pages are unloaded.
    /// </summary>
    internal void Reset()
    {
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        PdfSnapSurface surface = GetSurface(_lastRequest);
        surface.Reset();
        surface.DrawBackground(_lastRequest.BackgroundColor);
        Present(_lastRequest, CreateFrame(_lastRequest, []));
        _lastRequest = null;
    }

    private void RenderAll(PagesDrawingRequest request)
    {
        if (_disposed || request.RenderTarget == null)
        {
            return;
        }

        PdfSnapSurface surface = GetSurface(request);
        _frameContext = CreateFrameContext(request);
        surface.Reset();
        surface.UpdateContext(_frameContext);

        if (!request.Pages.Any(page => surface.IsCoveredByPage(page, request)))
        {
            surface.DrawBackground(request.BackgroundColor);
        }

        foreach (RequestPageInfo page in request.Pages)
        {
            DrawPage(surface, page, request, GetPageDrawFlags(PageDrawFlags.AllContent, request));
        }

        PdfPanelFrame panelFrame = CreateFrame(request, request.Pages);
        DrawPanelGraphics(surface, panelFrame);
        Present(request, panelFrame);
    }

    /// <summary>
    /// Draws <paramref name="page"/> of <paramref name="request"/> with the parts of <paramref name="flags"/> and the
    /// graphics of <see cref="OnPanelPageRendered"/>.
    /// </summary>
    private void DrawPage(PdfSnapSurface surface, in RequestPageInfo page, PagesDrawingRequest request, PageDrawFlags flags)
    {
        Action<PdfSnapSurface, PdfPanelFramePage>? pageRendered = OnPanelPageRendered;
        Action<PdfSnapSurface>? drawPageGraphics = null;

        if (pageRendered != null)
        {
            PdfPanelFramePage framePage = CreateFramePage(page, request.Scale);
            drawPageGraphics = pageSurface => pageRendered(pageSurface, framePage);
        }

        surface.DrawPage(page, request, _graphics, GetShadow(request), flags, drawPageGraphics);
    }

    /// <summary>
    /// Draws the graphics of <see cref="OnPanelRendered"/> over the whole panel.
    /// </summary>
    private void DrawPanelGraphics(PdfSnapSurface surface, PdfPanelFrame panelFrame)
    {
        Action<PdfSnapSurface, PdfPanelFrame>? panelRendered = OnPanelRendered;

        if (panelRendered == null)
        {
            return;
        }

        surface.Save();
        panelRendered(surface, panelFrame);
        surface.Restore();
    }

    /// <summary>
    /// Draws the graphics of <see cref="OnPanelRendered"/> over <paramref name="page"/> alone, the part a partial page
    /// draw painted again.
    /// </summary>
    private void DrawPanelGraphicsOverPage(PdfSnapSurface surface, in RequestPageInfo page, PagesDrawingRequest request, PdfPanelFrame panelFrame)
    {
        Action<PdfSnapSurface, PdfPanelFrame>? panelRendered = OnPanelRendered;

        if (panelRendered == null)
        {
            return;
        }

        surface.Save();
        surface.ClipToPage(page, request);
        panelRendered(surface, panelFrame);
        surface.Restore();
    }

    /// <summary>
    /// Creates the frame of <paramref name="request"/> showing <paramref name="pages"/>: its size, the host mapping of
    /// its render target and those pages.
    /// </summary>
    private static PdfPanelFrame CreateFrame(PagesDrawingRequest request, RequestPageInfo[] pages)
    {
        var framePages = new PdfPanelFramePage[pages.Length];

        for (int pageIndex = 0; pageIndex < framePages.Length; pageIndex++)
        {
            framePages[pageIndex] = CreateFramePage(pages[pageIndex], request.Scale);
        }

        PdfMatrix hostToPanel = PdfMatrix.Identity;

        if (request.RenderTarget != null)
        {
            hostToPanel = request.RenderTarget.HostToPanel;
        }

        return new PdfPanelFrame(request.PanelSize, hostToPanel, framePages);
    }

    private static PdfPanelFramePage CreateFramePage(in RequestPageInfo page, float scale) => new(page, scale);

    /// <summary>
    /// Returns <paramref name="flags"/> without <see cref="PageDrawFlags.Thumbnail"/> when <paramref name="request"/>
    /// does not use thumbnails as placeholders.
    /// </summary>
    private static PageDrawFlags GetPageDrawFlags(PageDrawFlags flags, PagesDrawingRequest request)
        => (request.ThumbnailPlaceholderMode == PdfPanelThumbnailPlaceholderMode.None) ? flags & ~PageDrawFlags.Thumbnail : flags;

    private void OnPageUpdated(object? sender, PageUpdatedArgs args)
    {
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        if (!_lastRequest.Pages.Any(p => p.PageNumber == args.PageNumber))
        {
            return;
        }

        RequestPageInfo page = _lastRequest.GetPage(args.PageNumber);
        PdfSnapSurface surface = GetSurface(_lastRequest);
        PdfPanelFrame panelFrame = CreateFrame(_lastRequest, _lastRequest.Pages);
        DrawPageContent(surface, page, _lastRequest, panelFrame);
        Present(_lastRequest, panelFrame);
    }

    /// <summary>
    /// Draws <paramref name="page"/> of <paramref name="request"/> again, without its shadow, and the graphics of
    /// <see cref="OnPanelRendered"/> over it.
    /// </summary>
    private void DrawPageContent(PdfSnapSurface surface, in RequestPageInfo page, PagesDrawingRequest request, PdfPanelFrame panelFrame)
    {
        PageDrawFlags flags = GetPageDrawFlags(PageDrawFlags.Background | PageDrawFlags.Thumbnail | PageDrawFlags.Content, request);
        DrawPage(surface, page, request, flags);
        DrawPanelGraphicsOverPage(surface, page, request, panelFrame);
    }

    /// <summary>
    /// Returns the page shadow over the background of <paramref name="request"/>, created again when that background
    /// changes.
    /// </summary>
    private PageShadow GetShadow(PagesDrawingRequest request)
    {
        if (_shadow == null || _shadow.Background != request.BackgroundColor)
        {
            _shadow = new PageShadow(request.BackgroundColor);
        }

        return _shadow;
    }

    /// <summary>
    /// Creates the execution context the frame of <paramref name="request"/> is drawn with: its antialiasing, optional
    /// content states and annotation states.
    /// </summary>
    private PdfCommandExecutionContext CreateFrameContext(PagesDrawingRequest request)
    {
        PdfOptionalContentStates optionalContentStates = request.OptionalContentStates ?? _panelDocument.Document.OptionalContentProperties.GetDefaultStates();
        PdfCommandExecutionParameters parameters = new() { Antialias = request.Antialias };

        return new PdfCommandExecutionContext(
            _frameCache,
            parameters,
            _panelDocument.DocumentLocker,
            optionalContentStates,
            request.AnnotationStates,
            new PdfCancellationExecutionObserver(CancellationToken.None));
    }

    /// <summary>
    /// Returns the frame surface the size of the panel of <paramref name="request"/>, drawing onto the samples of its
    /// render target, created again when the render target gives other samples.
    /// </summary>
    private PdfSnapSurface GetSurface(PagesDrawingRequest request)
    {
        IPdfPanelRenderTarget renderTarget = request.RenderTarget ?? throw new InvalidOperationException("The request has no render target.");
        int width = Math.Max(1, (int)request.PanelSize.Width);
        int height = Math.Max(1, (int)request.PanelSize.Height);
        PdfImageSamples samples = renderTarget.GetFrameSamples(width, height);

        if (_frame == null || !ReferenceEquals(_frameSamples, samples))
        {
            _frame?.Dispose();
            _frame = new PdfSnapSurface(samples, _frameParameters, _frameContext, _panelDocument.Document.LoggerFactory.CreateLogger<PdfSnapSurface>());
            _frameSamples = samples;
        }

        return _frame;
    }

    private void Present(PagesDrawingRequest request, PdfPanelFrame panelFrame)
    {
        IPdfPanelRenderTarget? renderTarget = request.RenderTarget;

        if (renderTarget == null || _frame == null)
        {
            return;
        }

        renderTarget.Present(panelFrame);
    }

    /// <summary>
    /// Unsubscribes from <see cref="PdfPanelDocument.PageUpdated"/> and marks the renderer as disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _frame?.Dispose();
        _frameCache.Dispose();
        _panelDocument.PageUpdated -= OnPageUpdated;
    }
}
