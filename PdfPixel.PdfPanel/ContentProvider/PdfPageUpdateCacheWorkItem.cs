using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;

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
    public PdfPageUpdateCacheWorkItem(PdfPageCacheEntry cacheEntry, IPdfDocument document, object documentLocker, PagesDrawingRequest request, Action<PageUpdatedArgs>? onPageUpdated)
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
    public bool IsSkippable => false;

    /// <summary>
    /// The cache entry this work item will populate.
    /// </summary>
    public PdfPageCacheEntry CacheEntry { get; }

    /// <inheritdoc />
    public void Process()
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

        if (CacheEntry.Content.NeedsUpdate(_request))
        {
            using LockedContent<PdfCommandRecorder> contentRecording = CacheEntry.Content.ContentCommandRecording.GetContent();

            if (contentRecording.Content != null)
            {
                using SKPictureRecorder recorder = new();
                SKCanvas canvas = recorder.BeginRecording(SKRect.Create(CacheEntry.PageInfo.Width, CacheEntry.PageInfo.Height));
                using PdfCommandExecutionContext executionContext = new(
                    _request.CommandExecutionParameters,
                    _documentLocker,
                    _document.OptionalContentGroups,
                    _contentObserver,
                    canvas,
                    _request.ComputeRegionOfInterest(CacheEntry.PageNumber));

                PdfDocumentContentExtensions.RecordingToSkPicture(contentRecording.Content, executionContext);

                SKPicture? contentPicture = recorder.EndRecording();
                bool isPartialContent = executionContext.IsPartialContent;
                List<PdfCharacter> characters = PdfTextBlockFlattener.Flatten(executionContext.RootTextBlock);

                CacheEntry.Content.UpdateContent(contentPicture, _request, isPartialContent, characters);
                contentUpdated = true;
                contentIsPartial = isPartialContent;
            }
        }

        if (contentUpdated)
        {
            _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Content, contentIsPartial, CacheEntry.Content.LastRegionOfInterest));
        }

        var annotationRecordingUpdated = false;

        lock (_documentLocker)
        {
            if (CacheEntry.Annotations?.Length > 0
                && (!CacheEntry.AnnotationContent.ContentCommandRecording.HasContent
                    || CacheEntry.AnnotationContent.LastRequest == null
                    || CacheEntry.AnnotationContent.LastRequest.ActiveAnnotation != _request.ActiveAnnotation
                    || CacheEntry.AnnotationContent.LastRequest.ActiveAnnotationState != _request.ActiveAnnotationState))
            {
                PdfCommandRecorder? annotationRecording = _document.GetAnnotationRecording(
                    CacheEntry.PageNumber,
                    _request.ActiveAnnotation?.PageAnnotation,
                    _request.ActiveAnnotationState,
                    _request.RenderingParameters,
                    _contentObserver);
                CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
                annotationRecordingUpdated = true;
            }// TODO: [MEDIUM] explore capabilities for partial update of annotation recording
        }

        if (CacheEntry.AnnotationContent.ContentCommandRecording.HasContent
            && (annotationRecordingUpdated || CacheEntry.AnnotationContent.NeedsUpdate(_request)))
        {
            using LockedContent<PdfCommandRecorder> contentRecording = CacheEntry.AnnotationContent.ContentCommandRecording.GetContent();

            var annotationIsPartial = false;

            if (contentRecording.Content != null)
            {
                using SKPictureRecorder annotationRecorder = new();
                SKCanvas annotationCanvas = annotationRecorder.BeginRecording(SKRect.Create(CacheEntry.PageInfo.Width, CacheEntry.PageInfo.Height));
                using PdfCommandExecutionContext annotationContext = new(
                    _request.CommandExecutionParameters,
                    _documentLocker,
                    _document.OptionalContentGroups,
                    _contentObserver,
                    annotationCanvas,
                    _request.ComputeRegionOfInterest(CacheEntry.PageNumber));

                PdfDocumentContentExtensions.RecordingToSkPicture(contentRecording.Content, annotationContext);

                SKPicture? annotationPicture = annotationRecorder.EndRecording();
                annotationIsPartial = annotationContext.IsPartialContent;

                CacheEntry.AnnotationContent.UpdateContent(annotationPicture, _request, annotationIsPartial);
            }

            _onPageUpdated?.Invoke(
                new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Annotations, annotationIsPartial, CacheEntry.AnnotationContent.LastRegionOfInterest));
        }
    }
}
