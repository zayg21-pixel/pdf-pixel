using PdfPixel.Commands;
using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Content;
using PdfPixel.Content.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.WorkQueue;
using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that decodes a page's content and annotation pictures and stores them in <see cref="CacheEntry"/>.
/// Invokes the page-updated callback when finished.
/// </summary>
internal sealed class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private static readonly PdfRenderingParameters RenderingParameters = new() { CacheDecodedTiles = true };

    private readonly object _documentLocker = new();
    private readonly IPdfDocument _document;
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
    /// Initializes the work item for the given cache entry and rendering request.
    /// Snapshots the current observers from the cache entry so replacements made by
    /// subsequent <see cref="PdfPageCacheEntry.InitializeForRendering"/> calls on the
    /// UI thread cannot affect this already-enqueued item.
    /// </summary>
    public PdfPageUpdateCacheWorkItem(
        PdfPageCacheEntry cacheEntry,
        IPdfDocument document,
        object documentLocker,
        PdfTextExtractor textExtractor,
        PdfTextExtractionProperties textExtractionProperties,
        PdfContentExtractor contentExtractor,
        PdfContentExtractionParameters contentExtractionParameters,
        PagesDrawingRequest request,
        Action<PageUpdatedArgs>? onPageUpdated,
        Action<int> onPageTextExtracted)
    {
        if (cacheEntry == null)
        {
            throw new ArgumentNullException(nameof(cacheEntry));
        }

        CacheEntry = cacheEntry;
        _documentLocker = documentLocker;
        _document = document;
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
        _parseObserver = cacheEntry.ParseObserver;
        _contentObserver = cacheEntry.ContentObserver;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The cache entry hands out a fresh content observer for every request, so holding one that is no
    /// longer the entry's current observer means a newer request for this page arrived while this item
    /// waited in the queue, and rendering the request it carries would paint the page at a scale the
    /// panel has already left.
    /// </remarks>
    public bool IsSkippable => !ReferenceEquals(_contentObserver, CacheEntry.ContentObserver);

    /// <summary>
    /// The cache entry this work item will populate.
    /// </summary>
    public PdfPageCacheEntry CacheEntry { get; }

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
            CacheEntry.CompleteRendering(_contentObserver);
        }
    }

    private async ValueTask ProcessContentAsync(IPdfCancellableExecutionObserver parseObserver, IPdfCancellableExecutionObserver contentObserver)
    {

        var contentUpdated = false;
        var contentIsPartial = false;

        lock (_documentLocker)
        {
            if (!CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                PdfCommandRecorder recording = _document.GeneratePageCommandRecording(CacheEntry.PageNumber, RenderingParameters, parseObserver);
                CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

        await parseObserver.YieldAsync().ConfigureAwait(false);

        if (CacheEntry.Content.NeedsPictureUpdate(_request))
        {
            PdfCommandRecorder? contentRecording;

            using (LockedContent<PdfCommandRecorder> lockedRecording = CacheEntry.Content.ContentCommandRecording.GetContent())
            {
                contentRecording = lockedRecording.Content;
            }

            if (contentRecording != null)
            {
                using SKPictureRecorder recorder = new();
                float pictureScale = CacheEntry.Content.GetPictureScale(_request);
                SKCanvas canvas = recorder.BeginRecording(SKRect.Create(CacheEntry.PageInfo.CropBox.Width * pictureScale, CacheEntry.PageInfo.CropBox.Height * pictureScale));
                using PdfCommandExecutionContext executionContext = new(
                    _document,
                    _executionParameters,
                    _documentLocker,
                    _request.OptionalContentStates,
                    contentObserver,
                    ToPictureRegion(_request.GetPage(CacheEntry.PageNumber).RegionOfInterest, pictureScale));

                await PdfDocumentContentExtensions
                    .RecordingToSkPictureAsync(contentRecording, executionContext, canvas, pictureScale, _document.LoggerFactory)
                    .ConfigureAwait(false);

                SKPicture? contentPicture = recorder.EndRecording();
                CacheEntry.Content.UpdateContent(contentPicture, _request);

                PdfMatrix pictureToPage = PdfMatrix.CreateScale(1f / pictureScale, 1f / pictureScale);

                if (!CacheEntry.Content.Words.HasContent)
                {
                    PdfWord[] words = _textExtractor.ExtractWords(executionContext.ContentTree.Root, pictureToPage, _textExtractionProperties);
                    CacheEntry.Content.Words.SetContent(words);
                    _onPageTextExtracted(CacheEntry.PageNumber);
                }

                if (!CacheEntry.Content.ContentItems.HasContent)
                {
                    PdfContentEntry[] entries = _contentExtractor.ExtractContent(executionContext.ContentTree.Root, _contentExtractionParameters);
                    CacheEntry.Content.ContentItems.SetContent(PdfDocumentContentExtensions.CreateContentItems(CacheEntry.PageNumber, entries, pictureToPage));
                }

                contentUpdated = true;
                contentIsPartial = (CacheEntry.Content.Features & PdfCommandFeatures.Region) != 0;
            }
        }

        if (contentUpdated)
        {
            _onPageUpdated?.Invoke(
                new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Content, contentIsPartial, _request.GetPage(CacheEntry.PageNumber).RegionOfInterest));

            await contentObserver.YieldAsync().ConfigureAwait(false);
        }

        var annotationRecordingUpdated = false;

        lock (_documentLocker)
        {
            if (CacheEntry.GetAnnotations(_document, _documentLocker).Length > 0
                && CacheEntry.AnnotationContent.NeedsAnnotationRecordingUpdate(_request))
            {
                PdfCommandRecorder? annotationRecording = _document.GetAnnotationRecording(
                    CacheEntry.PageNumber,
                    _request.ActiveAnnotation?.PageAnnotation,
                    _request.ActiveAnnotationState,
                    RenderingParameters,
                    contentObserver);
                CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
                annotationRecordingUpdated = true;
            }
        }

        if (CacheEntry.AnnotationContent.ContentCommandRecording.HasContent
            && (annotationRecordingUpdated || CacheEntry.AnnotationContent.NeedsPictureUpdate(_request)))
        {
            PdfCommandRecorder? annotationContentRecording;

            using (LockedContent<PdfCommandRecorder> lockedRecording = CacheEntry.AnnotationContent.ContentCommandRecording.GetContent())
            {
                annotationContentRecording = lockedRecording.Content;
            }

            var annotationIsPartial = false;

            if (annotationContentRecording != null)
            {
                using SKPictureRecorder annotationRecorder = new();
                float annotationPictureScale = CacheEntry.AnnotationContent.GetPictureScale(_request);
                SKCanvas annotationCanvas = annotationRecorder.BeginRecording(
                    SKRect.Create(CacheEntry.PageInfo.CropBox.Width * annotationPictureScale, CacheEntry.PageInfo.CropBox.Height * annotationPictureScale));
                using PdfCommandExecutionContext annotationContext = new(
                    _document,
                    _executionParameters,
                    _documentLocker,
                    _request.OptionalContentStates,
                    contentObserver,
                    ToPictureRegion(_request.GetPage(CacheEntry.PageNumber).RegionOfInterest, annotationPictureScale));

                await PdfDocumentContentExtensions
                    .RecordingToSkPictureAsync(annotationContentRecording, annotationContext, annotationCanvas, annotationPictureScale, _document.LoggerFactory)
                    .ConfigureAwait(false);

                SKPicture? annotationPicture = annotationRecorder.EndRecording();

                CacheEntry.AnnotationContent.UpdateContent(annotationPicture, _request);
                annotationIsPartial = (CacheEntry.AnnotationContent.Features & PdfCommandFeatures.Region) != 0;
            }

            _onPageUpdated?.Invoke(
                new PageUpdatedArgs(
                    CacheEntry.PageNumber,
                    CacheEntry.GetContentPictures(),
                    UpdatedContentType.Annotations,
                    annotationIsPartial,
                    _request.GetPage(CacheEntry.PageNumber).RegionOfInterest));
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
