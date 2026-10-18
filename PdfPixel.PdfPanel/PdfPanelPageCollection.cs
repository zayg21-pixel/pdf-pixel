using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Page collection for binding to <see cref="PdfPanelContext"/>.
/// </summary>
public sealed class PdfPanelPageCollection : ReadOnlyCollection<PdfPanelPage>, IDisposable
{
    private readonly object disposeLocker = new();
    private bool isDisposed;

    internal PdfPanelPageCollection(PdfPanelDocument panelDocument, PdfPageContentProvider contentProvider, IList<PdfPanelPage> pages)
        : base(pages)
    {
        PanelDocument = panelDocument;
        ContentProvider = contentProvider;
    }

    /// <summary>
    /// Document of the pages and the content produced for them.
    /// </summary>
    internal PdfPanelDocument PanelDocument { get; }

    /// <summary>
    /// Pages content provider that handles rendering of page content and annotation layers.
    /// </summary>
    internal PdfPageContentProvider ContentProvider { get; }

    /// <summary>
    /// Document the pages are decoded from.
    /// </summary>
    public IPdfDocument Document => PanelDocument.Document;

    /// <summary>
    /// Lock object serializing access to the document the pages are decoded from.
    /// Code reading the document while the pages are in use must lock on it.
    /// </summary>
    public object DocumentLocker => PanelDocument.DocumentLocker;

    /// <summary>
    /// Returns the page if it exists.
    /// </summary>
    /// <param name="pageNumber">Number of the page.</param>
    /// <param name="page">Viewer page.</param>
    /// <returns>True if page found.</returns>
    public bool TryGetPage(int pageNumber, out PdfPanelPage? page)
    {
        if (pageNumber < 1 || pageNumber > Count)
        {
            page = null;
            return false;
        }

        page = this[pageNumber - 1];
        return true;
    }

    /// <summary>
    /// Gets the annotation popup at the specified page position.
    /// </summary>
    /// <param name="pageNumber">Number of the page.</param>
    /// <param name="pagePosition">Position on the page in PDF coordinates.</param>
    /// <returns>The annotation popup if found; otherwise, null.</returns>
    public PdfAnnotationPopup? GetAnnotationPopupAt(int pageNumber, in PdfPoint pagePosition)
    {
        if (!TryGetPage(pageNumber, out PdfPanelPage? page) || page == null)
        {
            return null;
        }

        return PdfDocumentAnnotationExtractor.GetActiveAnnotation(page, pagePosition);
    }

    /// <summary>
    /// Generates <see cref="PdfPanelPageCollection"/> from PDF document.
    /// </summary>
    /// <param name="document">PDF document.</param>
    /// <param name="loggerFactory">Logger factory instance.</param>
    /// <param name="synchronizationContext">
    /// Synchronization context page content notifications are posted to, or <see langword="null"/> to raise them on the decoding thread.
    /// </param>
    /// <returns><see cref="PdfPanelPageCollection"/>.</returns>
    public static PdfPanelPageCollection FromDocument(IPdfDocument document, ILoggerFactory loggerFactory, SynchronizationContext? synchronizationContext)
        => FromDocument(document, new SequentialWorkQueue(loggerFactory.CreateLogger<SequentialWorkQueue>()), new PdfNonYieldingObserverFactory(), synchronizationContext);

    /// <summary>
    /// Generates <see cref="PdfPanelPageCollection"/> from PDF document.
    /// </summary>
    /// <param name="document">PDF document.</param>
    /// <param name="processingQueue">Queue the page content is decoded on.</param>
    /// <param name="observerFactory">Factory of the execution observers the page content is decoded with.</param>
    /// <param name="synchronizationContext">
    /// Synchronization context page content notifications are posted to, or <see langword="null"/> to raise them on the decoding thread.
    /// </param>
    /// <returns><see cref="PdfPanelPageCollection"/>.</returns>
    public static PdfPanelPageCollection FromDocument(
        IPdfDocument document,
        IWorkQueue processingQueue,
        IPdfExecutionObserverFactory observerFactory,
        SynchronizationContext? synchronizationContext)
    {
        PdfPanelDocument panelDocument = new(document, synchronizationContext);
        PdfPageContentProvider contentProvider = new(panelDocument, processingQueue, observerFactory);
        List<PdfPanelPage> pages = [];

        for (int i = 0; i < panelDocument.PageCount; i++)
        {
            int pageNumber = i + 1;
            PdfPanelPageInfo info = panelDocument.GetPageInfo(pageNumber);
            PdfPanelPage page = new(info, pageNumber, panelDocument);
            pages.Add(page);
        }

        return new PdfPanelPageCollection(panelDocument, contentProvider, pages);
    }

    /// <summary>
    /// Disposes the underlying content provider and releases all page resources.
    /// </summary>
    public void Dispose()
    {
        lock (disposeLocker)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            ContentProvider.Dispose();
            PanelDocument.Dispose();
        }
    }
}
