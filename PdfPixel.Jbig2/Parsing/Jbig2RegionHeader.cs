using PdfPixel.Jbig2.Decoding;

namespace PdfPixel.Jbig2.Parsing;

/// <summary>
/// Represents a region segment information field common to all region-type segments.
/// Defined in ITU-T T.88 Section 7.4.1.
/// </summary>
internal sealed class Jbig2RegionHeader
{
    public const int Jbig2RegionHeaderLength = 17;

    /// <summary>
    /// Region width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Region height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Horizontal location of the region relative to the page.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Vertical location of the region relative to the page.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Combination operator used when compositing this region onto the page buffer.
    /// </summary>
    public Jbig2CombinationOperator CombinationOperator { get; set; }
}
