using PdfPixel.Content;
using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel.Content;

/// <summary>
/// A content item placed on a page of the panel.
/// </summary>
public class PdfPanelContentItem
{
    /// <summary>
    /// Initializes a content item placed on the given 1-based page number.
    /// </summary>
    internal PdfPanelContentItem(int pageNumber, in PdfContentEntry entry, in PdfRectangle boundingBox)
    {
        PageNumber = pageNumber;
        Entry = entry;
        BoundingBox = boundingBox;
    }

    /// <summary>
    /// 1-based page number the item is placed on.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// The extracted content entry.
    /// </summary>
    public PdfContentEntry Entry { get; }

    /// <summary>
    /// Bounding box of the item in page coordinates.
    /// </summary>
    public PdfRectangle BoundingBox { get; }
}
