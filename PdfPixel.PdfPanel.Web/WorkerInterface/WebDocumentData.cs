using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// General document info available after initialization.
/// </summary>
public class WebDocumentData
{
    /// <summary>
    /// Number of pages in document.
    /// </summary>
    public int PagesCount { get; set; }

    /// <summary>
    /// Information about each individual page.
    /// </summary>
    public List<WebDocumentPageInfo> PageInfo { get; set; }

    /// <summary>
    /// Annotation popups for all pages, serialized from the worker-side document.
    /// </summary>
    public List<WebAnnotationPopupData> Annotations { get; set; }
}
