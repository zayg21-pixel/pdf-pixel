using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel;

public sealed class PdfRenderingQueue : IDisposable
{
    private const int DisposeWaitTimeout = 10000;
    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly IRenderLoopRunner _loopRunner;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly Thread _renderThread;
    private DrawingRequest _lastRequest;
    private volatile bool _disposed;

    // State preserved across iterations
    private SKSurface _surface;
    private SKSurface _thumbnailSurface;
    private int _thumbnailSurfaceSize;
    private PagesDrawingRequest _activePagesDrawingRequest;
    private PdfPanelRenderCommand _activeCommand;
    private PagesDrawingRequest _previousPagesDrawingRequest;
    private List<int> _backgroundRenderedForPages = new List<int>();
    private bool _requiresRedraw;

    public PdfRenderingQueue(ILoggerFactory loggerFactory, ISkSurfaceFactory surfaceFactory)
        : this(loggerFactory, surfaceFactory, new DefaultRenderLoopRunner())
    {
    }

    public PdfRenderingQueue(ILoggerFactory loggerFactory, ISkSurfaceFactory surfaceFactory, IRenderLoopRunner loopRunner)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PdfRenderingQueue>();
        _surfaceFactory = surfaceFactory;
        _loopRunner = loopRunner ?? throw new ArgumentNullException(nameof(loopRunner));
        _renderThread = new Thread(RenderThreadEntry)
        {
            IsBackground = true,
            Name = "PdfRenderingQueue"
        };
        _renderThread.Start();
    }

    private void RenderThreadEntry()
    {
        _surfaceFactory.Initialize();
        //_loopRunner.Start(RenderLoopIteration);
        _loopRunner.Start(RenderCommandLoopIteration);
    }

    internal void EnqueueDrawingRequest(DrawingRequest request)
    {
        if (_lastRequest != null && _lastRequest.Equals(request))
        {
            return;
        }

        _loopRunner.Enqueue(request);
        _lastRequest = request;
    }

    /// <summary>
    /// Single iteration of the render loop. Called by IRenderLoopRunner with the render frame.
    /// </summary>
    private void RenderCommandLoopIteration(RenderFrameCommand frame)
    {
        if (_disposed)
        {
            return;
        }

        var command = frame.Command;
        var cancellationToken = frame.CancellationToken;
        bool sameRequest = _activePagesDrawingRequest == command.DrawingRequest;
        _activePagesDrawingRequest = command.DrawingRequest;
        _activeCommand = command;

        if (_activePagesDrawingRequest != null)
        {
            EnsureSurfaceForRequest(_activePagesDrawingRequest, cancellationToken);
            EnsureThumbnailSurfaceForRequest(_activePagesDrawingRequest, cancellationToken);
        }

        try
        {
            switch (command.Type)
            {
                case PdfPanelRenderCommandType.Dispose:
                {
                    DisposeFromCommand();
                    break;
                }
                case PdfPanelRenderCommandType.DrawBackground:
                {
                    DrawBackground();
                    break;
                }
                case PdfPanelRenderCommandType.Render:
                {
                    _activePagesDrawingRequest.RenderTarget.Render(_surface, _activePagesDrawingRequest, cancellationToken);
                    break;
                }
                case PdfPanelRenderCommandType.InitializePage:
                {
                    InitializePage();
                    break;
                }
                case PdfPanelRenderCommandType.DrawThumbnail:
                {
                    DrawThumbnail();
                    break;
                }
                case PdfPanelRenderCommandType.GenerateContent:
                {
                    InitializePageContent(cancellationToken);
                    break;
                }
                case PdfPanelRenderCommandType.DrawContent:
                {
                    DrawPageContent();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred on execution of render command");
        }
        finally
        {
            if (!sameRequest)
            {
                _previousPagesDrawingRequest = _activePagesDrawingRequest;
            }
        }
    }

    private void DisposeFromCommand()
    {
        _disposed = true;
        _surface?.Dispose();
        _surface = null;
        _thumbnailSurface?.Dispose();
        _thumbnailSurface = null;
        _surfaceFactory.Dispose();
        _loopRunner.Stop();
    }

    private void EnsureSurfaceForRequest(PagesDrawingRequest request, CancellationToken cancellationToken)
    {
        var width = (int)request.CanvasSize.Width;
        var height = (int)request.CanvasSize.Height;
        if (_surface == null || _surface.Canvas.DeviceClipBounds.Width != width || _surface.Canvas.DeviceClipBounds.Height != height)
        {
            _surface = _surfaceFactory.GetDrawingSurface(width, height, cancellationToken);
        }
    }

    private void EnsureThumbnailSurfaceForRequest(PagesDrawingRequest request, CancellationToken cancellationToken)
    {
        if (request.MaxThumbnailSize > 0)
        {
            if (_thumbnailSurfaceSize != request.MaxThumbnailSize)
            {
                _thumbnailSurface = _surfaceFactory.CreateThumbnailSurface(request.MaxThumbnailSize, request.MaxThumbnailSize, cancellationToken);
                _thumbnailSurfaceSize = request.MaxThumbnailSize;
            }
        }
        else
        {
            _thumbnailSurface?.Dispose();
            _thumbnailSurface = null;
        }
    }

    private void DrawBackground()
    {
        _backgroundRenderedForPages.Clear();
        var canvas = _surface.Canvas;
        _surfaceFactory.SetCurrentSurface(_surface);

        if (_previousPagesDrawingRequest != null)
        {
            _surface.Flush();
            using var surfaceSnapshot = _surface.Snapshot();
            using var rasterSnapshot = surfaceSnapshot.ToRasterImage();
            DrawBackgroundAndShadows(canvas, _activePagesDrawingRequest);
            DrawExistingThumbnails(canvas, _activePagesDrawingRequest);
            RenderSurfaceSnapshot(canvas, rasterSnapshot, _activePagesDrawingRequest, _previousPagesDrawingRequest);
        }
        else
        {
            DrawBackgroundAndShadows(canvas, _activePagesDrawingRequest);
            DrawExistingThumbnails(canvas, _activePagesDrawingRequest);
        }
    }

    private void InitializePage()
    {
        var visiblePages = GetExtendedVisiblePages(_activePagesDrawingRequest, _previousPagesDrawingRequest);
        _activePagesDrawingRequest.Pages.UpdateCache(visiblePages);
        _surfaceFactory.SetCurrentSurface(_thumbnailSurface);

        _activePagesDrawingRequest.Pages.InitializePageWithThumbnail(_activeCommand.PageNumber.Value, _activePagesDrawingRequest.Scale, _thumbnailSurface, _activePagesDrawingRequest.ActiveAnnotation, _activePagesDrawingRequest.ActiveAnnotationState);
    }

    private void DrawThumbnail()
    {
        if (_backgroundRenderedForPages.Contains(_activeCommand.PageNumber.Value))
        {
            return;
        }
        var picture = _activePagesDrawingRequest.Pages.GetCachedPicture(_activeCommand.PageNumber.Value);
        _surfaceFactory.SetCurrentSurface(_surface);
        _surface.Canvas.DrawPageFromRequest(picture.PageNumber, _activePagesDrawingRequest, PageDrawFlags.Background | PageDrawFlags.Thumbnail);
    }

    private void InitializePageContent(CancellationToken token)
    {
        _surfaceFactory.SetCurrentSurface(_surface);
        _activePagesDrawingRequest.Pages.GeneratePicturesForPage(_activeCommand.PageNumber.Value, token);
    }

    private void DrawPageContent()
    {
        var picture = _activePagesDrawingRequest.Pages.GetCachedPicture(_activeCommand.PageNumber.Value);
        _surfaceFactory.SetCurrentSurface(_surface);
        _surface.Canvas.DrawPageFromRequest(picture.PageNumber, _activePagesDrawingRequest, PageDrawFlags.Background | PageDrawFlags.Content);
    }

    private static void DrawBackgroundAndShadows(SKCanvas canvas, PagesDrawingRequest request)
    {
        canvas.Clear(request.BackgroundColor);

        foreach (var page in request.VisiblePages)
        {
            canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Shadow);
        }
    }

    private static IEnumerable<int> GetExtendedVisiblePages(
        PagesDrawingRequest request,
        PagesDrawingRequest previousRequest)
    {
        var extendedVisiblePages = request.VisiblePages.Select(x => x.PageNumber);

        if (previousRequest != null && previousRequest.Scale == request.Scale)
        {
            if (previousRequest.Offset.Y <= request.Offset.Y && extendedVisiblePages.Any() && extendedVisiblePages.Max() != request.Pages.Count)
            {
                extendedVisiblePages = extendedVisiblePages.Append(extendedVisiblePages.Max() + 1);
            }
            else if (previousRequest.Offset.Y > request.Offset.Y && extendedVisiblePages.Any() && extendedVisiblePages.Min() != 1)
            {
                extendedVisiblePages = extendedVisiblePages.OrderByDescending(x => x).Append(extendedVisiblePages.Min() - 1);
            }
        }

        return extendedVisiblePages;
    }

    private void DrawExistingThumbnails(
        SKCanvas canvas,
        PagesDrawingRequest request)
    {
        foreach (var page in request.VisiblePages)
        {
            if (!request.Pages.TryGetPictureFromCache(page.PageNumber, out var cached))
            {
                continue;
            }

            if (cached.Thumbnail == null)
            {
                continue;
            }

            canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Thumbnail);
            _backgroundRenderedForPages.Add(page.PageNumber);
        }
    }

    private static void RenderSurfaceSnapshot(
        SKCanvas canvas,
        SKImage surfaceSnapshot,
        PagesDrawingRequest request,
        PagesDrawingRequest previousRequest)
    {
        if (surfaceSnapshot == null)
        {
            return;
        }

        foreach (var page in request.VisiblePages)
        {
            if (!previousRequest.VisiblePages.Any(x => x.PageNumber == page.PageNumber))
            {
                continue;
            }

            canvas.Save();

            var lastPage = previousRequest.VisiblePages.FirstOrDefault(x => x.PageNumber == page.PageNumber);
            var sourceRect = lastPage.GetScaledBounds(previousRequest.Scale);
            var destRect = page.GetScaledBounds(request.Scale);

            if (request.PageCornerRadius > 0)
            {
                using var clipPath = new SKPath();
                clipPath.AddRoundRect(destRect, request.PageCornerRadius * request.Scale, request.PageCornerRadius * request.Scale);
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias: true);
            }

            canvas.DrawImage(surfaceSnapshot, sourceRect, destRect);

            canvas.Restore();
        }
    }

    public void Dispose()
    {
        _loopRunner.Enqueue(new DisposeRequest());

        SpinWait.SpinUntil(() => _disposed, DisposeWaitTimeout);
        _loopRunner.Dispose();
    }
}
