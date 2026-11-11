using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Content;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Snap;
using PdfPixel.Text;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// Work item that records a page's content and annotation commands and rasterizes the tiles of its visible region
/// into the page's cache entry. Invokes the page-updated callback once, when the tiles are done.
/// </summary>
internal sealed class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private static readonly PdfRenderingParameters RenderingParameters = new();

    private readonly PdfPageContentTiler? _tiler;
    private readonly PdfTextExtractor _textExtractor;
    private readonly PdfTextExtractionProperties _textExtractionProperties;
    private readonly PdfContentExtractor _contentExtractor;
    private readonly PdfContentExtractionParameters _contentExtractionParameters;
    private readonly PdfSnapSurfaceParameters _surfaceParameters;
    private readonly ContentUpdateRequest _request;
    private readonly ContentUpdatePage _requestPage;
    private readonly Action<PageUpdatedArgs>? _onPageUpdated;
    private readonly Action<int> _onPageTextExtracted;
    private readonly IPdfCancellableExecutionObserver? _parseObserver;
    private readonly IPdfCancellableExecutionObserver? _contentObserver;

    /// <summary>
    /// Initializes the work item for the given page of a content update request.
    /// Snapshots the current observers from the cache entry so replacements made by
    /// subsequent <see cref="PdfPageCacheEntry.InitializeForRendering"/> calls on the
    /// UI thread cannot affect this already-enqueued item.
    /// </summary>
    public PdfPageUpdateCacheWorkItem(
        ContentUpdatePage requestPage,
        PdfPageContentTiler? tiler,
        PdfTextExtractor textExtractor,
        PdfTextExtractionProperties textExtractionProperties,
        PdfContentExtractor contentExtractor,
        PdfContentExtractionParameters contentExtractionParameters,
        PdfSnapSurfaceParameters surfaceParameters,
        ContentUpdateRequest request,
        Action<PageUpdatedArgs>? onPageUpdated,
        Action<int> onPageTextExtracted)
    {
        _requestPage = requestPage ?? throw new ArgumentNullException(nameof(requestPage));
        _tiler = tiler;
        _textExtractor = textExtractor;
        _textExtractionProperties = textExtractionProperties;
        _contentExtractor = contentExtractor;
        _contentExtractionParameters = contentExtractionParameters;
        _surfaceParameters = surfaceParameters ?? throw new ArgumentNullException(nameof(surfaceParameters));
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _onPageUpdated = onPageUpdated;
        _onPageTextExtracted = onPageTextExtracted ?? throw new ArgumentNullException(nameof(onPageTextExtracted));
        _parseObserver = _requestPage.Page.CacheEntry.ParseObserver;
        _contentObserver = _requestPage.Page.CacheEntry.ContentObserver;
    }

    /// <inheritdoc />
    public bool IsSkippable => !ReferenceEquals(_contentObserver, _requestPage.Page.CacheEntry.ContentObserver);

    /// <inheritdoc />
    public async ValueTask ProcessAsync()
    {
        if (_parseObserver == null || _contentObserver == null)
        {
            return;
        }

        await ProcessContentAsync(_parseObserver, _contentObserver).ConfigureAwait(false);
    }

    private async ValueTask ProcessContentAsync(IPdfCancellableExecutionObserver parseObserver, IPdfCancellableExecutionObserver contentObserver)
    {
        lock (_requestPage.Page.PanelDocument.DocumentLocker)
        {
            if (!_requestPage.Page.CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                PdfCommandRecorder recording = _requestPage.Page.PanelDocument.Document.GeneratePageCommandRecording(_requestPage.Page.PageNumber, RenderingParameters, parseObserver);
                _requestPage.Page.CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

        await parseObserver.YieldAsync().ConfigureAwait(false);

        lock (_requestPage.Page.PanelDocument.DocumentLocker)
        {
            if (_requestPage.Page.CacheEntry.GetAnnotations(_requestPage.Page).Length > 0
                && !_requestPage.Page.CacheEntry.AnnotationContent.ContentCommandRecording.HasContent)
            {
                PdfCommandRecorder? annotationRecording = _requestPage.Page.PanelDocument.Document.GetAnnotationRecording(
                    _requestPage.Page.PageNumber,
                    RenderingParameters,
                    contentObserver);
                _requestPage.Page.CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
            }
        }

        var tilesUpdate = TilesUpdateResult.Unchanged;

        if (_tiler != null)
        {
            tilesUpdate = await _tiler.UpdateTilesAsync(_requestPage, _request, contentObserver).ConfigureAwait(false);
        }

        bool needsWords = !_requestPage.Page.CacheEntry.Content.Words.HasContent;
        bool needsContentItems = !_requestPage.Page.CacheEntry.Content.ContentItems.HasContent;

        if (needsWords || needsContentItems)
        {
            await ExtractContentAsync(extractsWords: needsWords, extractsContentItems: needsContentItems, contentObserver).ConfigureAwait(false);
        }

        if (tilesUpdate == TilesUpdateResult.Updated)
        {
            _onPageUpdated?.Invoke(new PageUpdatedArgs(_requestPage.Page.PageNumber, UpdatedContentType.Content));
        }
    }

    /// <summary>
    /// Follows the page's content recording on a surface without pixels and stores the words and content items its
    /// content tree collected, each only when asked for.
    /// </summary>
    private async ValueTask ExtractContentAsync(bool extractsWords, bool extractsContentItems, IPdfCancellableExecutionObserver contentObserver)
    {
        PdfCommandRecorder? recording;

        using (LockedContent<PdfCommandRecorder> lockedRecording = _requestPage.Page.CacheEntry.Content.ContentCommandRecording.GetContent())
        {
            recording = lockedRecording.Content;
        }

        if (recording == null)
        {
            return;
        }

        PdfCommandExecutionContext executionContext = _requestPage.Page.CreateContentExecutionContext(_request.OptionalContentStates, contentObserver);

        using (PdfSnapSurface surface = new(0, 0, _surfaceParameters, executionContext, _requestPage.Page.PanelDocument.Document.LoggerFactory.CreateLogger<PdfSnapSurface>()))
        {
            await recording.ReplayAsync(surface).ConfigureAwait(false);
        }

        if (extractsWords)
        {
            PdfWord[] words = _textExtractor.ExtractWords(executionContext.ContentTree.Root, PdfMatrix.Identity, _textExtractionProperties);
            _requestPage.Page.CacheEntry.Content.Words.SetContent(words);
            _onPageTextExtracted(_requestPage.Page.PageNumber);
        }

        if (extractsContentItems)
        {
            PdfContentEntry[] entries = _contentExtractor.ExtractContent(executionContext.ContentTree.Root, _contentExtractionParameters);
            _requestPage.Page.CacheEntry.Content.ContentItems.SetContent(PdfDocumentContentExtensions.CreateContentItems(_requestPage.Page.PageNumber, entries, PdfMatrix.Identity));
        }
    }
}
