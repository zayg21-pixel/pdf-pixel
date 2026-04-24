using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.WorkQueue;
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
    private readonly object disposeLocker = new object();
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
    public bool TryGetPage(int pageNumber, out PdfPanelPage page)
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
    public PdfAnnotationPopup GetAnnotationPopupAt(int pageNumber, SKPoint pagePosition)
    {
        // TODO: use cached annotations instead
        return null;
        //if (!TryGetPage(pageNumber, out var page))
        //{
        //    return null;
        //}

        //var activeAnnotation = Document.GetActiveAnnotation(pageNumber, pagePosition);
        //if (activeAnnotation == null)
        //{
        //    return null;
        //}

        //foreach (var popup in page.Popups)
        //{
        //    if (popup.Annotation == activeAnnotation)
        //    {
        //        return popup;
        //    }
        //}

        //return null;
    }

    /// <summary>
    /// Generates <see cref="PdfPanelPageCollection"/> from PDF document.
    /// </summary>
    /// <param name="document">PDF document.</param>
    /// <returns><see cref="PdfPanelPageCollection"/>.</returns>
    public static PdfPanelPageCollection FromDocument(PdfDocument document)
    {
        var pages = new List<PdfPanelPage>();
        var contentProvider = new PdfPageContentProvider(document, new AsyncMultiProcessWorkQueue<PdfPageUpdateCacheWorkItem>(document.LoggerFactory.CreateLogger<AsyncMultiProcessWorkQueue<PdfPageUpdateCacheWorkItem>>()));

        for (int i = 0; i < document.Pages.Count; i++)
        {
            var pageNumber = i + 1;
            var info = contentProvider.GetPageInfo(pageNumber);
            var popups = document.CreateAnnotationPopups(pageNumber);
            var page = new PdfPanelPage(info, pageNumber, popups);
            pages.Add(page);
        }

        return new PdfPanelPageCollection(contentProvider, pages);
    }

    public static PdfPanelPageCollection FromContentProvider(IPdfPageContentProvider contentProvider)
    {
        var pages = new List<PdfPanelPage>();

        for (int i = 0; i < contentProvider.GetPagesCount(); i++)
        {
            var pageNumber = i + 1;
            var info = contentProvider.GetPageInfo(pageNumber);
            //var popups = document.CreateAnnotationPopups(pageNumber);
            var page = new PdfPanelPage(info, pageNumber, null);
            pages.Add(page);
        }

        return new PdfPanelPageCollection(contentProvider, pages);
    }

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
