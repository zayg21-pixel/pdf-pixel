using PdfPixel.Commands;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel;

public class SingleThreadedRenderLoopRunner : IRenderLoopRunner
{
    private readonly IPdfPageContentProvider _contentProvider;
    private readonly SynchronizationContext _syncContext;
    private bool _disposed;
    private CancellationTokenSource _skippableCts = new CancellationTokenSource();
    private Action<RenderFrameCommand> _iteration;
    private DrawingRequest _lastRequest;

    public SingleThreadedRenderLoopRunner(IPdfPageContentProvider contentProvider)
    {
        _contentProvider = contentProvider;
        _syncContext = SynchronizationContext.Current;
    }

    /// <inheritdoc />
    public void Start(Action<RenderFrameCommand> iteration)
    {
        _iteration = iteration ?? throw new ArgumentNullException(nameof(iteration));
        _iteration(new RenderFrameCommand(new PdfPanelRenderCommand(PdfPanelRenderCommandType.Initialize), CancellationToken.None));
    }
    /// <inheritdoc />
    public void Stop()
    {
        // No-op since this runner is single-threaded and processes commands immediately.
    }
    /// <inheritdoc />
    public async void Enqueue(DrawingRequest request)
    {
        if (_disposed)
        {
            return;
        }

        _lastRequest = request;
        var contentProvider = _contentProvider;

        if (contentProvider == null)
        {
            return;
        }

        var commands = PdfPanelRenderCommand.GenerateCommandsFromRequestNew(request, contentProvider);
        // Use a shared CancellationTokenSource for skippable work. Replacing it
        // cancels any previously queued/processing skippable frames so that
        // the current frame will observe cancellation after the next yield.
        var newCts = new CancellationTokenSource();
        var oldCts = _skippableCts;
        _skippableCts = newCts;
        oldCts?.Cancel();
        oldCts?.Dispose();

        if (request is PagesDrawingRequest pagesDrawingRequest)
        {
            contentProvider.RefreshCache(pagesDrawingRequest.VisiblePages.Select(x => x.PageNumber), newCts);

            foreach (var visiblePage in pagesDrawingRequest.VisiblePages)
            {
                var commandContext = new PdfCommandExecutionContext(pagesDrawingRequest.RenderingParameters, newCts.Token);
                var contentRequest = new ContentProviderRequest
                {
                    PageNumber = visiblePage.PageNumber,
                    RenderingParameters = pagesDrawingRequest.RenderingParameters,
                    CancellationTokenSource = newCts,
                    OnPageUpdated = RequestReRender
                };
                contentProvider.UpdateContent(contentRequest);
            }
        }

        // Non-skippable requests receive CancellationToken.None so they are never cancelled.
        var token = request.IsSkippable ? newCts.Token : CancellationToken.None; // TODO: we can do it smarter here, cancel ONLY page requests that are not valid anymore.

        try
        {
            foreach (var command in commands)
            {
                var frame = new RenderFrameCommand(command, token);
                _iteration(frame);
            }
        }
        catch
        {

        }
    }

    private void RequestReRender(int pageNumber, ContentLocker<SKPicture> pictureContent)
    {
        //_skippableCts?.Dispose();
        if (_syncContext == null)
        {
            RequestReRenderSync(pageNumber, pictureContent);
        }
        else
        {
            _syncContext.Send(_ =>
            {
                RequestReRenderSync(pageNumber, pictureContent);
            }, null);
        }
    }

    private bool RequestReRenderSync(int pageNumber, ContentLocker<SKPicture> pictureContent)
    {
        if (_disposed)
        {
            return false;
        }

        if (_lastRequest is PagesDrawingRequest lastPagesDrawingRequest &&
            lastPagesDrawingRequest.VisiblePages.Select(x => x.PageNumber).Contains(pageNumber))
        {
            var token = CancellationToken.None; // TODO: use token wisely
            var renderPageCommand = new PdfPanelRenderCommand(PdfPanelRenderCommandType.DrawContent, lastPagesDrawingRequest, pageNumber, pictureContent);
            var renderCommand = new PdfPanelRenderCommand(PdfPanelRenderCommandType.Render, lastPagesDrawingRequest);

            var renderPageFrameCommand = new RenderFrameCommand(renderPageCommand, token);
            _iteration(renderPageFrameCommand);

            var renderFrameCommand = new RenderFrameCommand(renderCommand, token);
            _iteration(renderFrameCommand);
        }

        return true;
    }


    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var cts = _skippableCts;
        _skippableCts = null;
        cts?.Cancel();
        cts?.Dispose();
    }
}
