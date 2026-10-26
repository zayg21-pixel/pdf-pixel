using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Scheduling;
using PdfPixel.PdfPanel.Thumbnails;
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
/// decoding via <see cref="PdfPanelScheduler"/>. When decoding completes, individual pages are
/// re-rendered on the UI thread without redrawing the whole panel.
/// </summary>
public sealed partial class PdfPanelRenderer : IDisposable
{
    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfPanelScheduler _scheduler;
    private readonly PdfPanelGraphics _graphics;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly Timer _contentUpdateTimer;
    private PdfPageContentTiler _tiler;
    private PdfAnimationClock _clock;
    private PagesDrawingRequest? _lastRequest;
    private PagesDrawingRequest? _lastLookAheadRequest;
    private long _lastTick;
    private bool _contentUpdatePending;
    private bool _disposed;

    /// <summary>
    /// Initializes the renderer, subscribes to <see cref="PdfPanelDocument.PageUpdated"/>, and calls <see cref="ISkSurfaceFactory.Initialize"/>.
    /// Animation and delayed content update callbacks are posted to the synchronization context of <paramref name="panelDocument"/>,
    /// or invoked on the calling thread when it has none.
    /// </summary>
    internal PdfPanelRenderer(
        ISkSurfaceFactory surfaceFactory,
        PdfPanelDocument panelDocument,
        PdfPanelScheduler scheduler,
        PdfPanelGraphics graphics)
    {
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _synchronizationContext = panelDocument.SynchronizationContext;
        _tiler = new PdfPageContentTiler(surfaceFactory, TileSize);
        _clock = new PdfAnimationClock(AnimationFps);
        _contentUpdateTimer = new Timer(OnContentUpdateDue, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        _panelDocument.PageUpdated += OnPageUpdated;
        _surfaceFactory.Initialize();
    }

    /// <summary>
    /// Renders the current cache state immediately, then starts background decoding for visible pages.
    /// A look-ahead request is not rendered; its pages are decoded after the visible ones.
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

        if (request.IsLookAhead)
        {
            SubmitLookAhead(request);
            return;
        }

        if (!request.Force && request.Equals(_lastRequest))
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
            StartContentUpdate(includeLookAhead: false);
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

        foreach (RequestPageInfo page in _lastRequest.Pages)
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
        _lastLookAheadRequest = null;
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

        _tiler.EvictExcept(request.Pages);

        foreach (RequestPageInfo page in request.Pages)
        {
            PdfContentPictures pictures = _panelDocument.GetContentPictures(page.PageNumber);

            if (!_panelDocument.NeedsContentUpdate(page.PageNumber, request))
            {
                _tiler.UpdateTiles(pictures.Content, in page, request, forceClearVisible: false);
            }

            surface.Canvas.DrawPage(page, request, pictures, _tiler, _graphics, GetPageDrawFlags(PageDrawFlags.AllContent, request), animation);
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

        foreach (RequestPageInfo page in _lastRequest.Pages)
        {
            PdfContentPictures pictures = _panelDocument.GetContentPictures(page.PageNumber);

            if (!IsPageLoading(pictures, _lastRequest))
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
        PagesDrawingRequest? request = _lastRequest;
        bool anyLoading = request != null
            && request.ShowPageLoadingAnimation
            && request.Pages.Any(page => IsPageLoading(_panelDocument.GetContentPictures(page.PageNumber), request));

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

    /// <summary>
    /// Returns whether the page of <paramref name="pictures"/> has no decoded content and no thumbnail drawn in its place.
    /// </summary>
    private static bool IsPageLoading(PdfContentPictures pictures, PagesDrawingRequest request)
    {
        return pictures.Content?.HasContent != true
            && (request.ThumbnailPlaceholderMode == PdfPanelThumbnailPlaceholderMode.None || pictures.Thumbnail?.HasContent != true);
    }

    /// <summary>
    /// Returns <paramref name="flags"/> without <see cref="PageDrawFlags.Thumbnail"/> when <paramref name="request"/>
    /// does not use thumbnails as placeholders.
    /// </summary>
    private static PageDrawFlags GetPageDrawFlags(PageDrawFlags flags, PagesDrawingRequest request)
        => (request.ThumbnailPlaceholderMode == PdfPanelThumbnailPlaceholderMode.None) ? flags & ~PageDrawFlags.Thumbnail : flags;

    private bool NeedsImmediateContentUpdate(PagesDrawingRequest? previousRequest, PagesDrawingRequest request)
    {
        if (previousRequest == null)
        {
            return true;
        }

        RequestPageInfo[] previousPages = previousRequest.Pages;

        foreach (RequestPageInfo page in request.Pages)
        {
            bool pageAppeared = !previousPages.Any(previousPage => previousPage.PageNumber == page.PageNumber);

            if (pageAppeared || _panelDocument.NeedsAnnotationUpdate(page.PageNumber, request))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Starts decoding the pages of the last visible request, followed by the pages of the last look-ahead request
    /// when <paramref name="includeLookAhead"/> is set.
    /// </summary>
    private void StartContentUpdate(bool includeLookAhead)
    {
        if (_lastRequest == null)
        {
            return;
        }

        _contentUpdatePending = false;
        _contentUpdateTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _scheduler.UpdateContent(_lastRequest);

        if (includeLookAhead && _lastLookAheadRequest != null)
        {
            _scheduler.UpdateContent(_lastLookAheadRequest);
        }
    }

    /// <summary>
    /// Keeps <paramref name="request"/> and starts decoding its pages unless the visible pages wait for a delayed content update.
    /// </summary>
    private void SubmitLookAhead(PagesDrawingRequest request)
    {
        if (request.Equals(_lastLookAheadRequest))
        {
            return;
        }

        _lastLookAheadRequest = request;

        if (!_contentUpdatePending)
        {
            _scheduler.UpdateContent(request);
        }
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

        StartContentUpdate(includeLookAhead: true);
    }

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

        _tiler.UpdateTiles(args.ContentPictures.Content, in page, _lastRequest, forceClearVisible: true);

        SKSurface surface = GetSurface(_lastRequest);
        PageDrawFlags flags = GetPageDrawFlags(PageDrawFlags.Background | PageDrawFlags.Thumbnail | PageDrawFlags.Content, _lastRequest);
        surface.Canvas.DrawPage(page, _lastRequest, args.ContentPictures, _tiler, _graphics, flags, default);
        Present(surface, _lastRequest);
    }

    private void DrawPageContent(SKSurface surface, in RequestPageInfo page)
    {
        if (_lastRequest == null)
        {
            return;
        }

        PdfContentPictures pictures = _panelDocument.GetContentPictures(page.PageNumber);
        PageDrawFlags flags = GetPageDrawFlags(PageDrawFlags.Background | PageDrawFlags.Thumbnail | PageDrawFlags.Content, _lastRequest);
        surface.Canvas.DrawPage(page, _lastRequest, pictures, _tiler, _graphics, flags, default);
    }

    private SKSurface GetSurface(PagesDrawingRequest request)
        => _surfaceFactory.GetDrawingSurface((int)request.PanelSize.Width, (int)request.PanelSize.Height);

    private void Present(SKSurface surface, PagesDrawingRequest request)
    {
        IPdfPanelRenderTarget? renderTarget = request.RenderTarget;

        if (renderTarget == null)
        {
            return;
        }

        renderTarget.Render(surface, new PdfPanelFrame(request, renderTarget.HostToPanel, _panelDocument));
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
        _clock.Tick -= OnAnimationTick;
        _clock.Dispose();

        _contentUpdateTimer.Dispose();
        _tiler.Dispose();
        _panelDocument.PageUpdated -= OnPageUpdated;
    }
}
