using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Drives the rendering loop for a PDF panel.
/// On each <see cref="Submit"/> call it renders immediately from the current cache, then triggers background
/// decoding via <see cref="IPdfPageContentProvider"/>. When decoding completes, individual pages are
/// re-rendered on the UI thread without redrawing the whole viewport.
/// </summary>
public sealed class PdfPanelRenderer : IDisposable
{
    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly IPdfPageContentProvider _contentProvider;
    private readonly PdfAnimationClock? _clock;
    private readonly SynchronizationContext? _syncContext;
    private PagesDrawingRequest? _lastRequest;
    private long _lastTick;
    private bool _disposed;

    /// <summary>
    /// Initializes the renderer, registers the page-updated callback, and calls <see cref="ISkSurfaceFactory.Initialize"/>.
    /// </summary>
    public PdfPanelRenderer(ISkSurfaceFactory surfaceFactory, IPdfPageContentProvider contentProvider, PdfAnimationClock? clock, SynchronizationContext? syncContext)
    {
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _clock = clock;
        _syncContext = syncContext;
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

        _contentProvider.UpdateContent(new UpdateContentRequest
        {
            VisiblePages = ExpandVisiblePages(request.VisiblePages, _contentProvider.GetPagesCount()),
            RenderingParameters = request.RenderingParameters,
            ActiveAnnotation = request.ActiveAnnotation,
            PointerState = request.ActiveAnnotationState
        });

        UpdateClockSubscription();
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

        foreach (VisiblePageInfo page in request.VisiblePages)
        {
            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);
            surface.Canvas.DrawPage(page, request, pictures, PageDrawFlags.All, animation);
        }

        request.RenderTarget.Render(surface, request);
    }

    private void OnAnimationTick(object? sender, AnimationTickEventArgs args)
        => _syncContext?.Post(_ => OnAnimationTick(args.Tick), null);

    private void OnAnimationTick(long tick)
    {
        _lastTick = tick;
        if (_disposed || _lastRequest == null || _lastRequest.RenderTarget == null)
        {
            return;
        }

        SKSurface surface = GetSurface(_lastRequest);
        var anyRedrawn = false;

        AnimationState? animation = _clock != null ? new AnimationState(tick, _clock.Fps) : null;

        foreach (VisiblePageInfo page in _lastRequest.VisiblePages)
        {
            PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(page.PageNumber);

            if (pictures.Content?.HasContent == true)
            {
                continue;
            }

            surface.Canvas.DrawPage(page, _lastRequest, pictures, PageDrawFlags.Background | PageDrawFlags.Content | PageDrawFlags.Placeholder, animation);
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
        => _syncContext?.Post(_ => OnPageUpdatedSync(args), null);

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
        SKSurface surface = GetSurface(_lastRequest);
        surface.Canvas.DrawPage(page, _lastRequest, args.ContentPictures, PageDrawFlags.Background | PageDrawFlags.Content, null);
        _lastRequest.RenderTarget.Render(surface, _lastRequest);
    }

    private SKSurface GetSurface(PagesDrawingRequest request)
        => _surfaceFactory.GetDrawingSurface((int)request.CanvasSize.Width, (int)request.CanvasSize.Height);

    private static int[] ExpandVisiblePages(VisiblePageInfo[] visiblePages, int totalPages)
    {
        if (visiblePages == null || visiblePages.Length == 0)
        {
            return Array.Empty<int>();
        }

        int first = visiblePages.Min(p => p.PageNumber);
        int last = visiblePages.Max(p => p.PageNumber);

        List<int> expanded = new(visiblePages.Select(p => p.PageNumber));

        if (first > 1)
        {
            expanded.Add(first - 1);
        }

        if (last < totalPages)
        {
            expanded.Add(last + 1);
        }

        return [.. expanded];
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
        if (_clock != null)
        {
            _clock.Tick -= OnAnimationTick;
        }
        _contentProvider.OnPageUpdated = null;
    }
}

