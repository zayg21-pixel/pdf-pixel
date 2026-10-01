using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.Skia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Drives the rendering loop for a PDF panel.
/// On each <see cref="Submit(PagesDrawingRequest)"/> call it renders immediately from the current cache, then triggers background
/// decoding via <see cref="PdfPageContentProvider"/>. When decoding completes, individual pages are
/// re-rendered on the UI thread without redrawing the whole panel.
/// </summary>
public sealed partial class PdfPanelRenderer : IDisposable
{
    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly PdfPageContentProvider _contentProvider;
    private readonly PdfPanelGraphics _graphics;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly Timer _contentUpdateTimer;
    private PdfPageContentTiler _tiler;
    private PdfAnimationClock _clock;
    private PagesDrawingRequest? _lastRequest;
    private long _lastTick;
    private bool _contentUpdatePending;
    private bool _disposed;

    /// <summary>
    /// Initializes the renderer, registers the page-updated callback, and calls <see cref="ISkSurfaceFactory.Initialize"/>.
    /// Page and animation callbacks are posted to <paramref name="synchronizationContext"/>, or invoked on the calling thread when it is <see langword="null"/>.
    /// </summary>
    internal PdfPanelRenderer(
        ISkSurfaceFactory surfaceFactory,
        PdfPageContentProvider contentProvider,
        PdfPanelGraphics graphics,
        SynchronizationContext? synchronizationContext)
    {
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _synchronizationContext = synchronizationContext;
        _tiler = new PdfPageContentTiler(surfaceFactory, TileSize);
        _clock = new PdfAnimationClock(AnimationFps);
        _contentUpdateTimer = new Timer(OnContentUpdateDue, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        _contentProvider.OnPageUpdated = OnPageUpdated;
        _surfaceFactory.Initialize();
    }

    /// <summary>
    /// Renders the current cache state immediately, then starts background decoding for visible pages.
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

        PagesDrawingRequest? previousRequest = _lastRequest;

        RenderAll(request);
        _lastRequest = request;

        TimeSpan contentUpdateDelay = (previousRequest != null && previousRequest.Scale != request.Scale)
            ? ZoomContentUpdateDelay
            : ScrollContentUpdateDelay;

        if (contentUpdateDelay <= TimeSpan.Zero || NeedsImmediateContentUpdate(previousRequest, request))
        {
            StartContentUpdate();
        }
        else
        {
            _contentUpdatePending = true;
            _contentUpdateTimer.Change(contentUpdateDelay, Timeout.InfiniteTimeSpan);
        }

        UpdateClockSubscription();
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

        SKSurface surface = GetSurface(_lastRequest);
        var pageDrawn = false;

        foreach (VisiblePageInfo page in _lastRequest.VisiblePages)
        {
            if (!pageNumbers.Contains(page.PageNumber))
            {
                continue;
            }

            DrawPageContent(surface, page);
            pageDrawn = true;
        }

        if (pageDrawn)
        {
            Present(surface, _lastRequest);
        }
    }

    /// <summary>
    /// Applies the <see cref="TileSize"/> and <see cref="AnimationFps"/> values that tiling and animation depend on.
    /// </summary>
    internal void Synchronize()
    {
        if (_disposed)
        {
            return;
        }

        if (_tiler.TileSize != TileSize)
        {
            _tiler.Dispose();
            _tiler = new PdfPageContentTiler(_surfaceFactory, TileSize);
        }

        if (_clock.Fps != AnimationFps)
        {
            _clock.Tick -= OnAnimationTick;
            _clock.Dispose();
            _clock = new PdfAnimationClock(AnimationFps);
            UpdateClockSubscription();
        }
    }

    /// <summary>
    /// Re-presents the current surface without redrawing page content. Used for overlay-only updates.
    /// </summary>
    internal void Present()
    {
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        Present(GetSurface(_lastRequest), _lastRequest);
    }

    /// <summary>
    /// Clears the surface and presents the empty result. Called when pages are unloaded.
    /// </summary>
    internal void Reset()
    {
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        _clock.Tick -= OnAnimationTick;

        _contentUpdatePending = false;
        _contentUpdateTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        SKSurface surface = GetSurface(_lastRequest);
        surface.Canvas.Clear(SKColors.Transparent);
        Present(surface, _lastRequest);
        _lastRequest = null;
    }

    private void RenderAll(PagesDrawingRequest request)
    {
        if (_disposed || request.RenderTarget == null)
        {
            return;
        }

        SKSurface surface = GetSurface(request);
        surface.Canvas.Clear(request.BackgroundColor.ToSkiaColor());
        AnimationState animation = GetAnimationState();

        _tiler.EvictExcept(request.VisiblePages);

        foreach (VisiblePageInfo page in request.VisiblePages)
        {
            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);

            if (!_contentProvider.NeedsContentUpdate(page.PageNumber, request))
            {
                _tiler.UpdateTiles(pictures.Content, in page, request, forceClearVisible: false);
            }

            surface.Canvas.DrawPage(page, request, pictures, _tiler, _graphics, PageDrawFlags.AllContent, animation);
        }

        Present(surface, request);
    }

    private void OnAnimationTick(object? sender, AnimationTickEventArgs args)
    {
        if (_synchronizationContext != null)
        {
            _synchronizationContext.Post(_ => OnAnimationTick(args.Tick), null);
        }
        else
        {
            OnAnimationTick(args.Tick);
        }
    }

    private void OnAnimationTick(long tick)
    {
        _lastTick = tick;
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        SKSurface surface = GetSurface(_lastRequest);
        var anyRedrawn = false;

        AnimationState animation = GetAnimationState();

        foreach (VisiblePageInfo page in _lastRequest.VisiblePages)
        {
            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);

            if (pictures.Content?.HasContent == true)
            {
                continue;
            }

            surface.Canvas.DrawPage(page, _lastRequest, pictures, _tiler, _graphics, PageDrawFlags.Background | PageDrawFlags.Placeholder, animation);
            anyRedrawn = true;
        }

        if (anyRedrawn)
        {
            Present(surface, _lastRequest);
        }
        else
        {
            _clock.Tick -= OnAnimationTick;
        }
    }

    private AnimationState GetAnimationState() => new(_lastTick, _clock.Fps);

    private void UpdateClockSubscription()
    {
        bool anyLoading = _lastRequest != null
            && _lastRequest.ShowPageLoadingAnimation
            && _lastRequest.VisiblePages.Any(
                p => _contentProvider.GetExistingContentPictures(p.PageNumber).Content?.HasContent != true);

        if (anyLoading)
        {
            _clock.Tick -= OnAnimationTick;
            _clock.Tick += OnAnimationTick;
        }
        else
        {
            _clock.Tick -= OnAnimationTick;
        }
    }

    private bool NeedsImmediateContentUpdate(PagesDrawingRequest? previousRequest, PagesDrawingRequest request)
    {
        if (previousRequest == null)
        {
            return true;
        }

        VisiblePageInfo[] previousPages = previousRequest.VisiblePages;

        foreach (VisiblePageInfo page in request.VisiblePages)
        {
            bool pageAppeared = !previousPages.Any(previousPage => previousPage.PageNumber == page.PageNumber);

            if (pageAppeared || _contentProvider.NeedsAnnotationUpdate(page.PageNumber, request))
            {
                return true;
            }
        }

        return false;
    }

    private void StartContentUpdate()
    {
        if (_lastRequest == null)
        {
            return;
        }

        _contentUpdatePending = false;
        _contentUpdateTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _contentProvider.UpdateContent(_lastRequest);
    }

    private void OnContentUpdateDue(object? state)
    {
        if (_synchronizationContext != null)
        {
            _synchronizationContext.Post(OnContentUpdateDueSync, null);
        }
        else
        {
            OnContentUpdateDueSync(null);
        }
    }

    private void OnContentUpdateDueSync(object? state)
    {
        if (_disposed || !_contentUpdatePending)
        {
            return;
        }

        StartContentUpdate();
    }

    private void OnPageUpdated(PageUpdatedArgs args)
    {
        if (_synchronizationContext != null)
        {
            _synchronizationContext.Post(_ => OnPageUpdatedSync(args), null);
        }
        else
        {
            OnPageUpdatedSync(args);
        }
    }

    private void OnPageUpdatedSync(PageUpdatedArgs args)
    {
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        if (!_lastRequest.VisiblePages.Any(p => p.PageNumber == args.PageNumber))
        {
            return;
        }

        VisiblePageInfo page = _lastRequest.GetPage(args.PageNumber);

        _tiler.UpdateTiles(args.ContentPictures.Content, in page, _lastRequest, forceClearVisible: true);

        SKSurface surface = GetSurface(_lastRequest);
        surface.Canvas.DrawPage(page, _lastRequest, args.ContentPictures, _tiler, _graphics, PageDrawFlags.Background | PageDrawFlags.Content, default);
        Present(surface, _lastRequest);
    }

    private void DrawPageContent(SKSurface surface, in VisiblePageInfo page)
    {
        if (_lastRequest == null)
        {
            return;
        }

        PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);
        surface.Canvas.DrawPage(page, _lastRequest, pictures, _tiler, _graphics, PageDrawFlags.Background | PageDrawFlags.Content, default);
    }

    private SKSurface GetSurface(PagesDrawingRequest request)
        => _surfaceFactory.GetDrawingSurface((int)request.PanelSize.Width, (int)request.PanelSize.Height);

    private static void Present(SKSurface surface, PagesDrawingRequest request)
    {
        IPdfPanelRenderTarget? renderTarget = request.RenderTarget;

        if (renderTarget == null)
        {
            return;
        }

        renderTarget.Render(surface, new PdfPanelFrame(request, renderTarget.HostToPanel));
    }

    /// <summary>
    /// Unregisters the page-updated callback and marks the renderer as disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _clock.Tick -= OnAnimationTick;
        _clock.Dispose();

        _contentUpdateTimer.Dispose();
        _tiler.Dispose();
        _contentProvider.OnPageUpdated = null;
    }
}
