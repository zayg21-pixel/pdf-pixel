using System;
using System.Linq;
using System.Threading;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;

namespace PdfPixel.PdfPanel;

public class SingleThreadedRenderLoopRunner : IRenderLoopRunner
{
    private readonly IPdfPageContentProvider _contentProvider;
    private readonly SynchronizationContext _syncContext;
    private bool _disposed;
    private Action<PdfPanelRenderCommand> _iteration;
    private DrawingRequest _lastRequest;

    public SingleThreadedRenderLoopRunner(IPdfPageContentProvider contentProvider)
    {
        _contentProvider = contentProvider;
        _syncContext = SynchronizationContext.Current;
    }

    /// <inheritdoc />
    public void Start(Action<PdfPanelRenderCommand> iteration)
    {
        _iteration = iteration ?? throw new ArgumentNullException(nameof(iteration));
        _iteration(new PdfPanelRenderCommand(PdfPanelRenderCommandType.Initialize));
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

        try
        {
            foreach (var command in commands)
            {
                _iteration(command);
            }
        }
        catch
        {
        }

        if (request is PagesDrawingRequest pagesDrawingRequest)
        {
            var pages = pagesDrawingRequest.VisiblePages.Select(x => x.PageNumber).OrderBy(x => x).ToList();

            if (pages[0] != 1)
            {
                pages.Add(pages[0] - 1);
            }

            if (pages[pages.Count - 1] != contentProvider.GetPagesCount())
            {
                pages.Add(pages[pages.Count - 1] + 1);
            }

            contentProvider.OnPageUpdated = RequestReRender;

            var contentRequest = new UpdateContentRequest
            {
                VisiblePages = pages,
                RenderingParameters = pagesDrawingRequest.RenderingParameters,
                ActiveAnnotation = pagesDrawingRequest.ActiveAnnotation,
                PointerState = pagesDrawingRequest.ActiveAnnotationState
            };

            contentProvider.UpdateContent(contentRequest);
        }
    }

    private void RequestReRender(PageUpdatedArgs args)
    {
        if (_syncContext == null)
        {
            RequestReRenderSync(args);
        }
        else
        {
            _syncContext.Send(_ =>
            {
                RequestReRenderSync(args);
            }, null);
        }
    }

    private bool RequestReRenderSync(PageUpdatedArgs args)
    {
        if (_disposed)
        {
            return false;
        }

        if (_lastRequest is PagesDrawingRequest lastPagesDrawingRequest &&
            lastPagesDrawingRequest.VisiblePages.Select(x => x.PageNumber).Contains(args.PageNumber))
        {
            var renderPageCommand = new PdfPanelRenderCommand(PdfPanelRenderCommandType.DrawContent, lastPagesDrawingRequest, args.PageNumber, args.ContentPictures);
            var renderCommand = new PdfPanelRenderCommand(PdfPanelRenderCommandType.Render, lastPagesDrawingRequest);

            _iteration(renderPageCommand);

            _iteration(renderCommand);
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
    }
}
