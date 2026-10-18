using PdfPixel.Content;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.WorkQueue;
using System;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// Produces page content, words and thumbnails for the requests of a panel and stores them in its <see cref="PdfPanelDocument"/>.
/// </summary>
internal sealed partial class PdfPanelScheduler : IDisposable
{
    private readonly PdfPanelDocument _panelDocument;
    private readonly IWorkQueue _processingQueue;
    private readonly IPdfExecutionObserverFactory _observerFactory;
    private readonly PdfTextExtractor _textExtractor = new();
    private readonly PdfTextExtractionProperties _textExtractionProperties = new();

    /// <summary>
    /// Initializes the scheduler for <paramref name="panelDocument"/>.
    /// </summary>
    public PdfPanelScheduler(PdfPanelDocument panelDocument, IWorkQueue processingQueue, IPdfExecutionObserverFactory observerFactory)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _observerFactory = observerFactory ?? throw new ArgumentNullException(nameof(observerFactory));
        _processingQueue = processingQueue;
    }

    /// <summary>
    /// Clears the extracted content of every page; <see cref="PdfPanelDocument.ExtractedContentCleared"/> is raised when done.
    /// </summary>
    public void ClearExtractedContent() => EnqueueClear(CachedContentType.Words | CachedContentType.Thumbnail);

    private void EnqueueClear(CachedContentType contentTypes)
        => _processingQueue.Enqueue(new PdfPageClearCacheWorkItem(_panelDocument.Entries, _panelDocument.DocumentLocker, contentTypes, OnExtractedContentCleared));

    private void OnExtractedContentCleared(CachedContentType contentTypes)
    {
        _panelDocument.NotifyExtractedContentCleared(contentTypes);
        EnqueueNextTextExtraction();
        EnqueueNextThumbnail();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SetTextExtractionSettings(false);
        CancelThumbnails();
        _processingQueue.Dispose();
    }
}
