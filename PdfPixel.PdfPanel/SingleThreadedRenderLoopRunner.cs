using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
            contentProvider.OnPageUpdated = RequestReRender;

            var contentRequest = new UpdateContentRequest
            {
                VisiblePages = ExpandVisiblePages(pagesDrawingRequest.VisiblePages, contentProvider.GetPagesCount()),
                RenderingParameters = pagesDrawingRequest.RenderingParameters,
                ActiveAnnotation = pagesDrawingRequest.ActiveAnnotation,
                PointerState = pagesDrawingRequest.ActiveAnnotationState
            };

            contentProvider.UpdateContent(contentRequest);
        }
    }

    private static int[] ExpandVisiblePages(VisiblePageInfo[] visiblePages, int totalPages)
    {
        if (visiblePages == null || visiblePages.Length == 0)
            return [];

        int first = visiblePages.Min(p => p.PageNumber);
        int last = visiblePages.Max(p => p.PageNumber);

        var expanded = new List<int>(visiblePages.Select(p => p.PageNumber));

        if (first > 1) expanded.Add(first - 1);
        if (last < totalPages) expanded.Add(last + 1);

        return expanded.ToArray();
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
