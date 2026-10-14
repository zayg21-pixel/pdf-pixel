using PdfPixel.Content.Model;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;

namespace PdfPixel.PdfPanel.Content;

/// <summary>
/// An image placed on a page of the panel.
/// </summary>
public sealed class PdfPanelImageContentItem : PdfPanelContentItem
{
    /// <summary>
    /// Initializes an image placed on the given 1-based page number.
    /// </summary>
    internal PdfPanelImageContentItem(int pageNumber, in PdfContentEntry entry, in PdfRectangle boundingBox, PdfImage image)
        : base(pageNumber, entry, boundingBox)
    {
        Image = image;
    }

    /// <summary>
    /// The placed image.
    /// </summary>
    public PdfImage Image { get; }
}
