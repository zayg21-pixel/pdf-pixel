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
    private PagesDrawingRequest _activePagesDrawingRequest;
    private PdfPanelRenderCommand _activeCommand;
    private PagesDrawingRequest _previousPagesDrawingRequest;
    private List<int> _backgroundRenderedForPages = new List<int>();

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
                    var surface = GetDrawingSurface(cancellationToken);
                    DrawBackground(surface);
                    break;
                }
                case PdfPanelRenderCommandType.Render:
                {
                    var surface = GetDrawingSurface(cancellationToken);
                    _activePagesDrawingRequest.RenderTarget.Render(surface, _activePagesDrawingRequest, cancellationToken);
                    break;
                }
                case PdfPanelRenderCommandType.InitializePage:
                {
                    var thumbnailSurface = GetThumbnailSurface(cancellationToken);
                    InitializePage(thumbnailSurface);
                    break;
                }
                case PdfPanelRenderCommandType.DrawThumbnail:
                {
                    var surface = GetDrawingSurface(cancellationToken);
                    DrawThumbnail(surface);
                    break;
                }
                case PdfPanelRenderCommandType.GenerateContent:
                {
                    InitializePageContent(cancellationToken);
                    break;
                }
                case PdfPanelRenderCommandType.DrawContent:
                {
                    var surface = GetDrawingSurface(cancellationToken);
                    DrawPageContent(surface);
                    break;
                }
                case PdfPanelRenderCommandType.Reset:
                {
                    if (_activePagesDrawingRequest == null)
                    {
                        return;
                    }

                    var surface = GetDrawingSurface(cancellationToken);
                    surface.Canvas.Clear(SKColors.Transparent);
                    _activePagesDrawingRequest.RenderTarget.Render(surface, _activePagesDrawingRequest, cancellationToken);

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
        _surfaceFactory.Dispose();
        _loopRunner.Stop();
    }

    private SKSurface GetDrawingSurface(CancellationToken cancellationToken)
    {
        var width = (int)_activePagesDrawingRequest.CanvasSize.Width;
        var height = (int)_activePagesDrawingRequest.CanvasSize.Height;
        return _surfaceFactory.GetDrawingSurface(width, height, cancellationToken);
    }

    private SKSurface GetThumbnailSurface(CancellationToken cancellationToken)
    {
        var size = _activePagesDrawingRequest.MaxThumbnailSize;
        return _surfaceFactory.GetThumbnailSurface(size, size, cancellationToken);
    }

    private void DrawBackground(SKSurface surface)
    {
        _backgroundRenderedForPages.Clear();
        var canvas = surface.Canvas;

        var visiblePages = _activePagesDrawingRequest.VisiblePages.Select(x => x.PageNumber);
        _activePagesDrawingRequest.Pages.UpdateCache(visiblePages);

        if (_previousPagesDrawingRequest != null)
        {
            surface.Flush();
            using var surfaceSnapshot = surface.Snapshot();
            DrawBackgroundAndShadows(canvas, _activePagesDrawingRequest);
            DrawExistingThumbnails(canvas, _activePagesDrawingRequest);
            RenderSurfaceSnapshot(canvas, surfaceSnapshot, _activePagesDrawingRequest, _previousPagesDrawingRequest);
        }
        else
        {
            DrawBackgroundAndShadows(canvas, _activePagesDrawingRequest);
            DrawExistingThumbnails(canvas, _activePagesDrawingRequest);
        }
    }

    private void InitializePage(SKSurface thumbnailSurface)
    {
        _activePagesDrawingRequest.Pages.InitializePageWithThumbnail(_activeCommand.PageNumber.Value, _activePagesDrawingRequest.Scale, thumbnailSurface, _activePagesDrawingRequest.ActiveAnnotation, _activePagesDrawingRequest.ActiveAnnotationState);
    }

    private void DrawThumbnail(SKSurface surface)
    {
        if (_backgroundRenderedForPages.Contains(_activeCommand.PageNumber.Value))
        {
            return;
        }
        var picture = _activePagesDrawingRequest.Pages.GetCachedPicture(_activeCommand.PageNumber.Value);
        surface.Canvas.DrawPageFromRequest(picture.PageNumber, _activePagesDrawingRequest, PageDrawFlags.Background | PageDrawFlags.Thumbnail);
    }

    private void InitializePageContent(CancellationToken token)
    {
        _activePagesDrawingRequest.Pages.GeneratePicturesForPage(_activeCommand.PageNumber.Value, token);
    }

    private void DrawPageContent(SKSurface surface)
    {
        var picture = _activePagesDrawingRequest.Pages.GetCachedPicture(_activeCommand.PageNumber.Value);
        surface.Canvas.DrawPageFromRequest(picture.PageNumber, _activePagesDrawingRequest, PageDrawFlags.Background | PageDrawFlags.Content);
    }

    private static void DrawBackgroundAndShadows(SKCanvas canvas, PagesDrawingRequest request)
    {
        canvas.Clear(request.BackgroundColor);

        foreach (var page in request.VisiblePages)
        {
            canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Shadow);
        }
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
