using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// General document info available after initialization.
/// </summary>
public class WebDocumentData : RequestResponseHeader
{
    /// <summary>
    /// Number of pages in document.
    /// </summary>
    public int PagesCount { get; set; }

    /// <summary>
    /// Information about each individual page.
    /// </summary>
    public List<WebDocumentPageInfo> PageInfo { get; set; }
}
