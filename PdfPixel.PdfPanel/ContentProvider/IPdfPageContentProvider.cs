using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Requests;
using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Provides decoded page content and annotation pictures for rendering.
/// Implementations run decoding on a background thread and notify via <see cref="OnPageUpdated"/>.
/// </summary>
public interface IPdfPageContentProvider : IDisposable
{
    /// <summary>
    /// Synchronisation object used to serialise access to the underlying PDF document.
    /// </summary>
    object DocumentLocker { get; }

    /// <summary>
    /// Called on the UI thread whenever a page's content or annotations have been decoded and are ready to render.
    /// </summary>
    Action<PageUpdatedArgs>? OnPageUpdated { get; set; }

    /// <summary>
    /// Returns the annotation popups for the specified 1-based page number.
    /// </summary>
    PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber);

    /// <summary>
    /// Returns the total number of pages in the document.
    /// </summary>
    int GetPagesCount();

    /// <summary>
    /// Returns the currently cached <see cref="PdfContentPictures"/> for the specified 1-based page number.
    /// Returns empty pictures if the page has not been decoded yet.
    /// </summary>
    PdfContentPictures GetExistingContentPictures(int pageNumber);

    /// <summary>
    /// Starts or updates background decoding for the pages described by <paramref name="request"/>.
    /// Pages no longer visible are cancelled and their cache cleared.
    /// </summary>
    void UpdateContent(PagesDrawingRequest request);

    /// <summary>
    /// Returns the <see cref="PdfPanelPageInfo"/> for the specified 1-based page number.
    /// </summary>
    PdfPanelPageInfo GetPageInfo(int pageNumber);
}
