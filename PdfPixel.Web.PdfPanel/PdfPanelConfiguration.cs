using SkiaSharp;

namespace PdfPixel.Web.PdfPanel;

/// <summary>
/// Strongly-typed configuration parsed from JS for the web PDF panel.
/// </summary>
internal struct PdfPanelConfiguration
{
    public float MinZoom { get; set; }

    public float MaxZoom { get; set; }

    public SKColor BackgroundColor { get; set; }

    public int MaxThumbnailSize { get; set; }

    public float MinimumPageGap { get; set; }

    public SKRect PagesPadding { get; set; }
}
