using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.Skia.Fonts;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Page collection for binding to <see cref="PdfPanelContext"/>.
/// </summary>
public sealed class PdfPanelPageCollection : ReadOnlyCollection<PdfPanelPage>, IDisposable
{
    private readonly object disposeLocker = new();
    private bool isDisposed;

    internal PdfPanelPageCollection(IPdfPageContentProvider contentProvider, IList<PdfPanelPage> pages)
        : base(pages)
    {
        ContentProvider = contentProvider;
    }

    /// <summary>
    /// Pages content provider that handles rendering of page content and annotation layers.
    /// </summary>
    public IPdfPageContentProvider ContentProvider { get; }

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
    /// <param name="fontSubstitutor">Font substitutor the document's substituted typefaces are matched back through.</param>
    /// <param name="loggerFactory">Logger factory instance.</param>
    /// <returns><see cref="PdfPanelPageCollection"/>.</returns>
    public static PdfPanelPageCollection FromDocument(IPdfDocument document, SkiaFontSubstitutor fontSubstitutor, ILoggerFactory loggerFactory)
    {
        PdfPageContentProvider contentProvider = new(document, fontSubstitutor, new ImmidiateWorkQueue(loggerFactory.CreateLogger<ImmidiateWorkQueue>()), new PdfYieldingObserverFactory());
        //PdfPageContentProvider contentProvider = new(document, fontSubstitutor, new AsyncWorkQueue(loggerFactory.CreateLogger<AsyncWorkQueue>()));

        return FromContentProvider(contentProvider);
    }

    /// <summary>
    /// Generates <see cref="PdfPanelPageCollection"/> from page content provider.
    /// </summary>
    /// <param name="contentProvider">Document content provider.</param>
    /// <returns><see cref="PdfPanelPageCollection"/>.</returns>
    public static PdfPanelPageCollection FromContentProvider(IPdfPageContentProvider contentProvider)
    {
        if (contentProvider == null)
        {
            throw new ArgumentNullException(nameof(contentProvider));
        }

        List<PdfPanelPage> pages = [];

        for (int i = 0; i < contentProvider.GetPagesCount(); i++)
        {
            int pageNumber = i + 1;
            PdfPanelPageInfo info = contentProvider.GetPageInfo(pageNumber);
            PdfAnnotationPopup[]? popups = contentProvider.GetAnnotationPopups(pageNumber);
            PdfPanelPage page = new(info, pageNumber, popups);
            pages.Add(page);
        }

        return new PdfPanelPageCollection(contentProvider, pages);
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
        }
    }
}
