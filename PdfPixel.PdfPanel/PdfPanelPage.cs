using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Represents a page in the PDF document in <see cref="PdfPanelPageCollection"/>.
/// </summary>
public class PdfPanelPage
{
    internal PdfPanelPage(PdfPanelPageInfo info, int pageNumber)
    {
        Info = info;
        PageNumber = pageNumber;
    }

    /// <summary>
    /// Information about the page, it's size and rotation.
    /// </summary>
    public PdfPanelPageInfo Info { get; set; }

    /// <summary>
    /// Number of the page.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the offset of the page.
    /// Offset is expressed in the scaled canvas space (affected by the current zoom factor) and
    /// represents the distance in device pixels from the top-left corner of the content area to
    /// the top-left corner of this page.
    /// </summary>
    public SKPoint Offset { get; set; }

    /// <summary>
    /// User defined rotation of the page in degrees.
    /// Can be any value that is a multiple of 90.
    /// </summary>
    public int UserRotation { get; set; }

    // TODO: both point and rotation should be moved to request
}
