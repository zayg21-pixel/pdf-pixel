using PdfPixel.Commands;
using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that decodes a page's content and annotation pictures and stores them in <see cref="CacheEntry"/>.
/// Invokes <see cref="IPdfPageContentProvider.OnPageUpdated"/> when finished.
/// </summary>
public class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private readonly object _documentLocker = new();
    private readonly IPdfDocument _document;
    private readonly PagesDrawingRequest _request;
    private readonly Action<PageUpdatedArgs>? _onPageUpdated;
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
        PagesDrawingRequest request,
        Action<PageUpdatedArgs>? onPageUpdated)
    {
        if (cacheEntry == null)
        {
            throw new ArgumentNullException(nameof(cacheEntry));
        }

        CacheEntry = cacheEntry;
        _documentLocker = documentLocker;
        _document = document;
        _request = request;
        _onPageUpdated = onPageUpdated;
        _parseObserver = cacheEntry.ParseObserver;
        _contentObserver = cacheEntry.ContentObserver;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The cache entry hands out a fresh content observer for every request, so holding one that is no
    /// longer the entry's current observer means a newer request for this page arrived while this item
    /// waited in the queue, and rendering the request it carries would paint the page at a scale the
    /// viewport has already left.
    /// </remarks>
    public bool IsSkippable => !ReferenceEquals(_contentObserver, CacheEntry.ContentObserver);

    /// <summary>
    /// The cache entry this work item will populate.
    /// </summary>
    public PdfPageCacheEntry CacheEntry { get; }

    /// <inheritdoc />
    public async ValueTask ProcessAsync()
    {
        if (_parseObserver == null ||  _contentObserver == null)
        {
            return;
        }

        var contentUpdated = false;
        var contentIsPartial = false;

        lock (_documentLocker)
        {
            if (!CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                PdfCommandRecorder recording = _document.GeneratePageCommandRecording(CacheEntry.PageNumber, _request.RenderingParameters, _parseObserver);
                CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

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
                    _request.CommandExecutionParameters,
                    _documentLocker,
                    _document.OptionalContentGroups,
                    _contentObserver,
                    ToPictureRegion(_request.GetPage(CacheEntry.PageNumber).RegionOfInterest, pictureScale));

                await PdfDocumentContentExtensions
                    .RecordingToSkPictureAsync(contentRecording, executionContext, canvas, pictureScale, _document.LoggerFactory)
                    .ConfigureAwait(false);

                SKPicture? contentPicture = recorder.EndRecording();
                List<PdfCharacter> characters = PdfTextBlockFlattener.Flatten(executionContext.RootTextBlock);

                CacheEntry.Content.UpdateContent(contentPicture, _request, characters);
                contentUpdated = true;
                contentIsPartial = (CacheEntry.Content.Features & PdfCommandFeatures.Region) != 0;
            }
        }

        if (contentUpdated)
        {
            _onPageUpdated?.Invoke(
                new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Content, contentIsPartial, _request.GetPage(CacheEntry.PageNumber).RegionOfInterest));
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
                    _request.RenderingParameters,
                    _contentObserver);
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
                    _request.CommandExecutionParameters,
                    _documentLocker,
                    _document.OptionalContentGroups,
                    _contentObserver,
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
    private static PdfRectangle ToPictureRegion(in PdfRectangle regionOfInterest, float pictureScale)
        => PdfMatrix.CreateScale(pictureScale, pictureScale).MapRect(regionOfInterest);
}
