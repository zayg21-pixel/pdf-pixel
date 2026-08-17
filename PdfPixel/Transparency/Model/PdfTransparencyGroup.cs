using PdfPixel.Color.ColorSpace;

namespace PdfPixel.Transparency.Model;

/// <summary>
/// Represents a PDF transparency group.
/// </summary>
public class PdfTransparencyGroup
{
    /// <summary>
    /// Converter for the group's blending color space (CS), or <see langword="null"/> when it does not resolve.
    /// </summary>
    public PdfColorSpaceConverter? ColorSpaceConverter { get; set; }

    /// <summary>
    /// Isolated flag (I). When true, the group composites against a transparent backdrop.
    /// </summary>
    public bool Isolated { get; set; }

    /// <summary>
    /// Knockout flag (K). Parsed but not yet applied during compositing.
    /// </summary>
    public bool Knockout { get; set; }
}
