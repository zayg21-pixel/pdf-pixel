using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Information about a page in a PDF document.
/// </summary>
public readonly struct PdfPanelPageInfo
{
    /// <summary>
    /// Initializes page info with the given label, crop box, and PDF rotation.
    /// </summary>
    public PdfPanelPageInfo(string label, in PdfRectangle cropBox, int rotation)
    {
        Label = label;
        CropBox = cropBox;
        Rotation = rotation;
    }

    /// <summary>
    /// Human-readable page label (e.g. "iv", "1", "A-1") as defined in the PDF.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Visible page area without rotation, in PDF coordinates (bottom-left origin, Y-up).
    /// </summary>
    public PdfRectangle CropBox { get; }

    /// <summary>
    /// Page rotation in degrees.
    /// </summary>
    public int Rotation { get; }
}
