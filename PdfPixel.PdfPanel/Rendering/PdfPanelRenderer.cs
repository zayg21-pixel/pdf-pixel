using PdfPixel.Commands;
using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Text;
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
    private readonly SynchronizationContext? _syncContext;
    private PagesDrawingRequest? _lastRequest;
    private UserInterfaceDrawingRequest? _lastUserInterfaceRequest;
    private long _lastTick;
    private bool _disposed;

    /// <summary>
    /// Text selection state and highlight renderer for the panel.
    /// </summary>
    public PdfPanelTextSelector TextSelector { get; }

    /// <summary>
    /// Initializes the renderer, registers the page-updated callback, and calls <see cref="ISkSurfaceFactory.Initialize"/>.
    /// </summary>
    public PdfPanelRenderer(ISkSurfaceFactory surfaceFactory, IPdfPageContentProvider contentProvider, PdfAnimationClock? clock, SynchronizationContext? syncContext)
    {
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _tiler = new PdfPageContentTiler(surfaceFactory);
        _clock = clock;
        _syncContext = syncContext;
        TextSelector = new PdfPanelTextSelector(contentProvider);
        _contentProvider.OnPageUpdated = OnPageUpdated;
        _surfaceFactory.Initialize();
    }

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

        _lastRequest = request;
        RenderAll(request);

        _contentProvider.UpdateContent(request);

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
            VisiblePageInfo page = _lastRequest.VisiblePages.First(p => p.PageNumber == pointerPagePosition.Value.PageNumber);

            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);

            if (pictures.Content?.HasContent == true)
            {
                SKSurface surface = GetSurface(_lastRequest);
                surface.Canvas.DrawPage(page, _lastRequest, pictures, _tiler, TextSelector, PageDrawFlags.Background | PageDrawFlags.Content, default);
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

        SKPoint pointerPosition = request.PointerPosition.Value;

        foreach (VisiblePageInfo page in request.VisiblePages)
        {
            SKMatrix canvasToContent = page.GetContentToCanvasMatrix(request.Scale).Invert();
            SKPoint contentPoint = canvasToContent.MapPoint(pointerPosition);

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
        surface.Canvas.Clear(request.BackgroundColor);
        AnimationState? animation = (_clock != null) ? new AnimationState(_lastTick, _clock.Fps) : null;

        _tiler.EvictExcept(request.VisiblePages);

        foreach (VisiblePageInfo page in request.VisiblePages)
        {
            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);

            // When it's scale/region dependent, we will receive OnPageUpdated event and redraw the page 
            if (!((pictures.ContentFeatures & PdfCommandFeatures.Scale) != 0 || (pictures.ContentFeatures & PdfCommandFeatures.Region) != 0))
            {
                _tiler.UpdateTiles(page.PageNumber, pictures.Content, in page, in request, forceClearVisible: false);
            }

            surface.Canvas.DrawPage(page, request, pictures, _tiler, TextSelector, PageDrawFlags.All, animation);
        }

        request.RenderTarget.Render(surface, request);
    }

    private void OnAnimationTick(object? sender, AnimationTickEventArgs args)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => OnAnimationTick(args.Tick), null);
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

            surface.Canvas.DrawPage(page, _lastRequest, pictures, _tiler, TextSelector, PageDrawFlags.Background | PageDrawFlags.Content | PageDrawFlags.Placeholder, animation);
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

    private void OnPageUpdated(PageUpdatedArgs args)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => OnPageUpdatedSync(args), null);
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

        VisiblePageInfo page = _lastRequest.VisiblePages.First(p => p.PageNumber == args.PageNumber);

        _tiler.UpdateTiles(page.PageNumber, args.ContentPictures.Content, in page, in _lastRequest, forceClearVisible: true);

        SKSurface surface = GetSurface(_lastRequest);
        surface.Canvas.DrawPage(page, _lastRequest, args.ContentPictures, _tiler, TextSelector, PageDrawFlags.Background | PageDrawFlags.Content, null);
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

        TextSelector.Dispose();
        _tiler.Dispose();
        _contentProvider.OnPageUpdated = null;
    }
}

