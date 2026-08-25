using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Text;
using PdfPixel.Skia;
using SkiaSharp;
using System;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Drives the rendering loop for a PDF panel.
/// On each <see cref="Submit(PagesDrawingRequest)"/> call it renders immediately from the current cache, then triggers background
/// decoding via <see cref="IPdfPageContentProvider"/>. When decoding completes, individual pages are
/// re-rendered on the UI thread without redrawing the whole viewport.
/// </summary>
public sealed class PdfPanelRenderer : IDisposable
{
    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly IPdfPageContentProvider _contentProvider;
    private readonly PdfPageContentTiler _tiler;
    private readonly PdfAnimationClock? _clock;
    private readonly Timer? _contentUpdateTimer;
    private PagesDrawingRequest? _lastRequest;
    private UserInterfaceDrawingRequest? _lastUserInterfaceRequest;
    private long _lastTick;
    private bool _contentUpdatePending;
    private bool _disposed;

    /// <summary>
    /// Initializes the renderer, registers the page-updated callback, and calls <see cref="ISkSurfaceFactory.Initialize"/>.
    /// </summary>
    public PdfPanelRenderer(ISkSurfaceFactory surfaceFactory, IPdfPageContentProvider contentProvider, PdfAnimationClock? clock, PdfPanelRendererProperties properties)
    {
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _tiler = new PdfPageContentTiler(surfaceFactory, properties.TileSize);
        _clock = clock;
        TextSelector = new PdfPanelTextSelector(contentProvider, properties.TextSelectorParameters);

        if (properties.ContentUpdateDelay > TimeSpan.Zero)
        {
            _contentUpdateTimer = new Timer(OnContentUpdateDue, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        _contentProvider.OnPageUpdated = OnPageUpdated;
        _surfaceFactory.Initialize();
    }

    /// <summary>
    /// Text selection state and highlight renderer for the panel.
    /// </summary>
    public PdfPanelTextSelector TextSelector { get; }

    /// <summary>
    /// Configuration values the renderer was created with.
    /// </summary>
    public PdfPanelRendererProperties Properties { get; }

    /// <summary>
    /// Renders the current cache state immediately, then starts background decoding for visible pages.
    /// </summary>
    public void Submit(PagesDrawingRequest request)
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

        if (_contentUpdateTimer == null || NeedsImmediateContentUpdate(previousRequest, request))
        {
            StartContentUpdate();
        }
        else
        {
            _contentUpdatePending = true;
            _contentUpdateTimer.Change(Properties.ContentUpdateDelay, Timeout.InfiniteTimeSpan);
        }

        UpdateClockSubscription();
    }

    /// <summary>
    /// Submits a user interface drawing request. Re-renders when the pointer is over a visible page
    /// and the request has changed.
    /// </summary>
    public void Submit(UserInterfaceDrawingRequest request)
    {
        if (_disposed)
        {
            return;
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.Equals(_lastUserInterfaceRequest))
        {
            return;
        }

        _lastUserInterfaceRequest = request;

        if (_lastRequest == null)
        {
            return;
        }

        PointerPagePosition? pointerPagePosition = GetPointerPagePosition(request);
        TextSelector.Update((request.ActiveAnnotation == null) ? pointerPagePosition : null);
        // TODO: [HIGH] currently annotations always wins, this shall, but it should be "first wins" logic,
        // for instance, if text is selected, navigation over same area shall not work

        if (pointerPagePosition != null && _lastRequest.RenderTarget != null)
        {
            VisiblePageInfo page = _lastRequest.GetPage(pointerPagePosition.Value.PageNumber);

            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);

            if (pictures.Content?.HasContent == true)
            {
                SKSurface surface = GetSurface(_lastRequest);
                surface.Canvas.DrawPage(page, _lastRequest, pictures, _tiler, TextSelector, Properties.PageCornerRadius, PageDrawFlags.Background | PageDrawFlags.Content, default);
                _lastRequest.RenderTarget.Render(GetSurface(_lastRequest), _lastRequest);
            }
        }
    }

    private static PointerPagePosition? GetPointerPagePosition(UserInterfaceDrawingRequest request)
    {
        if (request.PointerPosition == null)
        {
            return null;
        }

        PdfPoint pointerPosition = request.PointerPosition.Value;

        foreach (VisiblePageInfo page in request.VisiblePages)
        {
            PdfMatrix canvasToContent = page.GetContentToCanvasMatrix(request.Scale).Invert();
            PdfPoint contentPoint = canvasToContent.MapPoint(pointerPosition);

            if (contentPoint.X >= 0
                && contentPoint.X <= page.Info.Width
                && contentPoint.Y >= 0
                && contentPoint.Y <= page.Info.Height)
            {
                return new PointerPagePosition(page.PageNumber, contentPoint, request.PointerState);
            }
        }

        return null;
    }

    /// <summary>
    /// Re-presents the current surface without redrawing page content. Used for overlay-only updates.
    /// </summary>
    public void Refresh()
    {
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        _lastRequest.RenderTarget.Render(GetSurface(_lastRequest), _lastRequest);
    }

    /// <summary>
    /// Clears the surface and presents the empty result. Called when pages are unloaded.
    /// </summary>
    public void Reset()
    {
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        if (_clock != null)
        {
            _clock.Tick -= OnAnimationTick;
        }

        _contentUpdatePending = false;
        _contentUpdateTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        SKSurface surface = GetSurface(_lastRequest);
        surface.Canvas.Clear(SKColors.Transparent);
        _lastRequest.RenderTarget.Render(surface, _lastRequest);
        _lastRequest = null;
    }

    private void RenderAll(PagesDrawingRequest request)
    {
        if (_disposed || request.RenderTarget == null)
        {
            return;
        }

        SKSurface surface = GetSurface(request);
        surface.Canvas.Clear(Properties.BackgroundColor.ToSkiaColor());
        AnimationState? animation = (_clock != null) ? new AnimationState(_lastTick, _clock.Fps) : null;

        _tiler.EvictExcept(request.VisiblePages);

        foreach (VisiblePageInfo page in request.VisiblePages)
        {
            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);

            if (!_contentProvider.NeedsContentUpdate(page.PageNumber, request))
            {
                _tiler.UpdateTiles(pictures.Content, in page, request, forceClearVisible: false);
            }

            surface.Canvas.DrawPage(page, request, pictures, _tiler, TextSelector, Properties.PageCornerRadius, PageDrawFlags.AllContent, animation);
        }

        request.RenderTarget.Render(surface, request);
    }

    private void OnAnimationTick(object? sender, AnimationTickEventArgs args)
    {
        if (Properties.SynchronizationContext != null)
        {
            Properties.SynchronizationContext.Post(_ => OnAnimationTick(args.Tick), null);
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

        AnimationState? animation = (_clock != null) ? new AnimationState(tick, _clock.Fps) : null;

        foreach (VisiblePageInfo page in _lastRequest.VisiblePages)
        {
            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);

            if (pictures.Content?.HasContent == true)
            {
                continue;
            }

            surface.Canvas.DrawPage(page, _lastRequest, pictures, _tiler, TextSelector, Properties.PageCornerRadius, PageDrawFlags.Background | PageDrawFlags.Placeholder, animation);
            anyRedrawn = true;
        }

        if (anyRedrawn)
        {
            _lastRequest.RenderTarget.Render(surface, _lastRequest);
        }
        else if (_clock != null)
        {
            _clock.Tick -= OnAnimationTick;
        }
    }

    private void UpdateClockSubscription()
    {
        if (_clock == null)
        {
            return;
        }

        bool anyLoading = _lastRequest != null
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
        _contentUpdateTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _contentProvider.UpdateContent(_lastRequest);
    }

    private void OnContentUpdateDue(object? state)
    {
        if (Properties.SynchronizationContext != null)
        {
            Properties.SynchronizationContext.Post(OnContentUpdateDueSync, null);
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
        if (Properties.SynchronizationContext != null)
        {
            Properties.SynchronizationContext.Post(_ => OnPageUpdatedSync(args), null);
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
        surface.Canvas.DrawPage(page, _lastRequest, args.ContentPictures, _tiler, TextSelector, Properties.PageCornerRadius, PageDrawFlags.Background | PageDrawFlags.Content, null);
        _lastRequest.RenderTarget.Render(surface, _lastRequest);
    }

    private SKSurface GetSurface(PagesDrawingRequest request)
        => _surfaceFactory.GetDrawingSurface((int)request.CanvasSize.Width, (int)request.CanvasSize.Height);

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
        if (_clock != null)
        {
            _clock.Tick -= OnAnimationTick;
        }

        _contentUpdateTimer?.Dispose();
        TextSelector.Dispose();
        _tiler.Dispose();
        _contentProvider.OnPageUpdated = null;
    }
}

