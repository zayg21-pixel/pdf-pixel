using PdfPixel.PdfPanel.Caching;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Data for <see cref="PdfPanelDocument.PageUpdated"/>, raised when a page's decoded content is ready.
/// </summary>
internal sealed class PageUpdatedArgs
{
    /// <summary>
    /// Initialises a new instance with the updated page number and content type.
    /// </summary>
    public PageUpdatedArgs(int pageNumber, UpdatedContentType updatedContentType)
    {
        PageNumber = pageNumber;
        UpdatedContentType = updatedContentType;
    }

    /// <summary>
    /// 1-based number of the page that was updated.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Indicates whether the main content or only the annotation layer was updated.
    /// </summary>
    public UpdatedContentType UpdatedContentType { get; }
}
