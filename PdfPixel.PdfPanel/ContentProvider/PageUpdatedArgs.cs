namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Arguments passed to <see cref="IPdfPageContentProvider.OnPageUpdated"/> when a page's decoded content is ready.
/// </summary>
public class PageUpdatedArgs
{
    /// <summary>
    /// Initialises a new instance with the updated page number, pictures, and content type.
    /// </summary>
    public PageUpdatedArgs(int pageNumber, PdfContentPictures contentPictures, UpdatedContentType updatedContentType)
    {
        PageNumber = pageNumber;
        ContentPictures = contentPictures;
        UpdatedContentType = updatedContentType;
    }

    /// <summary>
    /// 1-based number of the page that was updated.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Freshly decoded content and annotation pictures ready for rendering.
    /// </summary>
    public PdfContentPictures ContentPictures { get; }

    /// <summary>
    /// Indicates whether the main content or only the annotation layer was updated.
    /// </summary>
    public UpdatedContentType UpdatedContentType { get; }
}
