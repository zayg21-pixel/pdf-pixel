using SkiaSharp;

namespace PdfPixel.Canvas;

/// <summary>
/// Represents a page in the PDF document in <see cref="PdfViewerPageCollection"/>.
/// </summary>
public class PdfViewerPage
{
    internal PdfViewerPage(PageInfo info, int pageNumber)
    {
        Info = info;
        PageNumber = pageNumber;
    }

    /// <summary>
    /// Information about the page, it's size and rotation.
    /// </summary>
    public PageInfo Info { get; set; }

    /// <summary>
    /// Number of the page.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the offset of the page.
    /// Page offset is the unscaled distance from the top-left corner of the page to the top-left corner of the document.
    /// </summary>
    public SKPoint Offset { get; internal set; }

    /// <summary>
    /// User defined rotation of the page in degrees.
    /// Can be any value that is a multiple of 90.
    /// </summary>
    public int UserRotation { get; set; }

    // TODO: both point and rotation should be moved to request
}
