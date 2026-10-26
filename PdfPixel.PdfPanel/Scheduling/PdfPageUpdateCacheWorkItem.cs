using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Content;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// Work item that decodes a page's content and annotation pictures and stores them in the page's cache entry.
/// Invokes the page-updated callback when finished.
/// </summary>
internal sealed class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private static readonly PdfRenderingParameters RenderingParameters = new() { CacheDecodedTiles = true };

    private readonly PdfPanelPage _page;
    private readonly PdfTextExtractor _textExtractor;
    private readonly PdfTextExtractionProperties _textExtractionProperties;
    private readonly PdfContentExtractor _contentExtractor;
    private readonly PdfContentExtractionParameters _contentExtractionParameters;
    private readonly PagesDrawingRequest _request;
    private readonly PdfCommandExecutionParameters _executionParameters;
    private readonly Action<PageUpdatedArgs>? _onPageUpdated;
    private readonly Action<int> _onPageTextExtracted;
    private readonly IPdfCancellableExecutionObserver? _parseObserver;
    private readonly IPdfCancellableExecutionObserver? _contentObserver;

    /// <summary>
    /// Initializes the work item for the given page and rendering request.
    /// Snapshots the current observers from the cache entry so replacements made by
    /// subsequent <see cref="PdfPageCacheEntry.InitializeForRendering"/> calls on the
    /// UI thread cannot affect this already-enqueued item.
    /// </summary>
    public PdfPageUpdateCacheWorkItem(
        PdfPanelPage page,
        PdfTextExtractor textExtractor,
        PdfTextExtractionProperties textExtractionProperties,
        PdfContentExtractor contentExtractor,
        PdfContentExtractionParameters contentExtractionParameters,
        PagesDrawingRequest request,
        Action<PageUpdatedArgs>? onPageUpdated,
        Action<int> onPageTextExtracted)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _textExtractor = textExtractor;
        _textExtractionProperties = textExtractionProperties;
        _contentExtractor = contentExtractor;
        _contentExtractionParameters = contentExtractionParameters;
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _executionParameters = new PdfCommandExecutionParameters
        {
            Antialias = request.Antialias,
            SnapToDevicePixels = request.SnapToDevicePixels
        };
        _onPageUpdated = onPageUpdated;
        _onPageTextExtracted = onPageTextExtracted ?? throw new ArgumentNullException(nameof(onPageTextExtracted));
        _parseObserver = _page.CacheEntry.ParseObserver;
        _contentObserver = _page.CacheEntry.ContentObserver;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The cache entry hands out a fresh content observer for every request, so holding one that is no
    /// longer the entry's current observer means a newer request for this page arrived while this item
    /// waited in the queue, and rendering the request it carries would paint the page at a scale the
    /// panel has already left.
    /// </remarks>
    public bool IsSkippable => !ReferenceEquals(_contentObserver, _page.CacheEntry.ContentObserver);

    /// <inheritdoc />
    public async ValueTask ProcessAsync()
    {
        if (_parseObserver == null || _contentObserver == null)
        {
            return;
        }

        try
        {
            await ProcessContentAsync(_parseObserver, _contentObserver).ConfigureAwait(false);
        }
        finally
        {
            _page.CacheEntry.CompleteRendering(_contentObserver);
        }
    }

    private async ValueTask ProcessContentAsync(IPdfCancellableExecutionObserver parseObserver, IPdfCancellableExecutionObserver contentObserver)
    {

        var contentUpdated = false;
        var contentIsPartial = false;

        lock (_page.PanelDocument.DocumentLocker)
        {
            if (!_page.CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                PdfCommandRecorder recording = _page.PanelDocument.Document.GeneratePageCommandRecording(_page.PageNumber, RenderingParameters, parseObserver);
                _page.CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

        await parseObserver.YieldAsync().ConfigureAwait(false);

        if (_page.CacheEntry.Content.NeedsPictureUpdate(_request))
        {
            PdfCommandRecorder? contentRecording;

            using (LockedContent<PdfCommandRecorder> lockedRecording = _page.CacheEntry.Content.ContentCommandRecording.GetContent())
            {
                contentRecording = lockedRecording.Content;
            }

            if (contentRecording != null)
            {
                using SKPictureRecorder recorder = new();
                float pictureScale = _page.CacheEntry.Content.GetPictureScale(_request);
                SKCanvas canvas = recorder.BeginRecording(SKRect.Create(_page.Size.Width * pictureScale, _page.Size.Height * pictureScale));
                using PdfCommandExecutionContext executionContext = new(
                    _page.PanelDocument.Document,
                    _executionParameters,
                    _page.PanelDocument.DocumentLocker,
                    _request.OptionalContentStates,
                    contentObserver,
                    ToPictureRegion(_request.GetPage(_page.PageNumber).RegionOfInterest, pictureScale));

                await PdfDocumentContentExtensions
                    .RecordingToSkPictureAsync(contentRecording, executionContext, canvas, pictureScale, _page.PanelDocument.Document.LoggerFactory)
                    .ConfigureAwait(false);

                SKPicture? contentPicture = recorder.EndRecording();
                _page.CacheEntry.Content.UpdateContent(contentPicture, _request);

                PdfMatrix pictureToPage = PdfMatrix.CreateScale(1f / pictureScale, 1f / pictureScale);

                if (!_page.CacheEntry.Content.Words.HasContent)
                {
                    PdfWord[] words = _textExtractor.ExtractWords(executionContext.ContentTree.Root, pictureToPage, _textExtractionProperties);
                    _page.CacheEntry.Content.Words.SetContent(words);
                    _onPageTextExtracted(_page.PageNumber);
                }

                if (!_page.CacheEntry.Content.ContentItems.HasContent)
                {
                    PdfContentEntry[] entries = _contentExtractor.ExtractContent(executionContext.ContentTree.Root, _contentExtractionParameters);
                    _page.CacheEntry.Content.ContentItems.SetContent(PdfDocumentContentExtensions.CreateContentItems(_page.PageNumber, entries, pictureToPage));
                }

                contentUpdated = true;
                contentIsPartial = (_page.CacheEntry.Content.Features & PdfCommandFeatures.Region) != 0;
            }
        }

        if (contentUpdated)
        {
            _onPageUpdated?.Invoke(
                new PageUpdatedArgs(_page.PageNumber, _page.CacheEntry.GetContentPictures(), UpdatedContentType.Content, contentIsPartial, _request.GetPage(_page.PageNumber).RegionOfInterest));

            await contentObserver.YieldAsync().ConfigureAwait(false);
        }

        var annotationRecordingUpdated = false;

        lock (_page.PanelDocument.DocumentLocker)
        {
            if (_page.CacheEntry.GetAnnotations(_page).Length > 0
                && _page.CacheEntry.AnnotationContent.NeedsAnnotationRecordingUpdate(_request))
            {
                PdfCommandRecorder? annotationRecording = _page.PanelDocument.Document.GetAnnotationRecording(
                    _page.PageNumber,
                    _request.ActiveAnnotation?.PageAnnotation,
                    _request.ActiveAnnotationState,
                    RenderingParameters,
                    contentObserver);
                _page.CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
                annotationRecordingUpdated = true;
            }
        }

        if (_page.CacheEntry.AnnotationContent.ContentCommandRecording.HasContent
            && (annotationRecordingUpdated || _page.CacheEntry.AnnotationContent.NeedsPictureUpdate(_request)))
        {
            PdfCommandRecorder? annotationContentRecording;

            using (LockedContent<PdfCommandRecorder> lockedRecording = _page.CacheEntry.AnnotationContent.ContentCommandRecording.GetContent())
            {
                annotationContentRecording = lockedRecording.Content;
            }

            var annotationIsPartial = false;

            if (annotationContentRecording != null)
            {
                using SKPictureRecorder annotationRecorder = new();
                float annotationPictureScale = _page.CacheEntry.AnnotationContent.GetPictureScale(_request);
                SKCanvas annotationCanvas = annotationRecorder.BeginRecording(
                    SKRect.Create(_page.Size.Width * annotationPictureScale, _page.Size.Height * annotationPictureScale));
                using PdfCommandExecutionContext annotationContext = new(
                    _page.PanelDocument.Document,
                    _executionParameters,
                    _page.PanelDocument.DocumentLocker,
                    _request.OptionalContentStates,
                    contentObserver,
                    ToPictureRegion(_request.GetPage(_page.PageNumber).RegionOfInterest, annotationPictureScale));

                await PdfDocumentContentExtensions
                    .RecordingToSkPictureAsync(annotationContentRecording, annotationContext, annotationCanvas, annotationPictureScale, _page.PanelDocument.Document.LoggerFactory)
                    .ConfigureAwait(false);

                SKPicture? annotationPicture = annotationRecorder.EndRecording();

                _page.CacheEntry.AnnotationContent.UpdateContent(annotationPicture, _request);
                annotationIsPartial = (_page.CacheEntry.AnnotationContent.Features & PdfCommandFeatures.Region) != 0;
            }

            _onPageUpdated?.Invoke(
                new PageUpdatedArgs(
                    _page.PageNumber,
                    _page.CacheEntry.GetContentPictures(),
                    UpdatedContentType.Annotations,
                    annotationIsPartial,
                    _request.GetPage(_page.PageNumber).RegionOfInterest));
        }
    }

    /// <summary>
    /// Maps <paramref name="regionOfInterest"/> from page content coordinates into the coordinates of
    /// a picture recorded at <paramref name="pictureScale"/>.
    /// </summary>
    private static PdfRectangle? ToPictureRegion(PdfRectangle? regionOfInterest, float pictureScale)
    {
        if (regionOfInterest == null)
        {
            return null;
        }

        return PdfMatrix.CreateScale(pictureScale, pictureScale).MapRect(regionOfInterest.Value);
    }
}
