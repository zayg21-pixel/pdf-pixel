using PdfPixel.Color;
using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Strongly-typed configuration parsed from JS for the web PDF panel.
/// </summary>
internal struct PdfPanelConfiguration
{
    public float MinZoom { get; set; }

    public float MaxZoom { get; set; }

    public PdfColor BackgroundColor { get; set; }

    public float MinimumPageGap { get; set; }

    public PdfRectangle PagesPadding { get; set; }
}
