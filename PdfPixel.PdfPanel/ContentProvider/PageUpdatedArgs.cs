using SkiaSharp;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Arguments passed to <see cref="IPdfPageContentProvider.OnPageUpdated"/> when a page's decoded content is ready.
/// </summary>
public class PageUpdatedArgs
{
    /// <summary>
    /// Initialises a new instance with the updated page number, pictures, content type, partial flag, and region of interest.
    /// </summary>
    public PageUpdatedArgs(int pageNumber, PdfContentPictures contentPictures, UpdatedContentType updatedContentType, bool isPartialContent, SKRect regionOfInterest)
    {
        PageNumber = pageNumber;
        ContentPictures = contentPictures;
        UpdatedContentType = updatedContentType;
        IsPartialContent = isPartialContent;
        RegionOfInterest = regionOfInterest;
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

    /// <summary>
    /// True when the content is a partial flush and further updates for this page are expected.
    /// </summary>
    public bool IsPartialContent { get; }

    /// <summary>
    /// Visible region within the page content coordinate space used when the content was decoded.
    /// </summary>
    public SKRect RegionOfInterest { get; }
}
