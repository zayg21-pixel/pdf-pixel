namespace PdfPixel.Imaging.Jbig2.Model;

/// <summary>
/// Page information parsed from a JBIG2 page information segment (type 48).
/// Contains the page dimensions and default pixel value.
/// </summary>
internal sealed class Jbig2PageInfo
{
    /// <summary>
    /// Page width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Page height in pixels. May be 0xFFFFFFFF for unknown (streaming mode).
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Horizontal resolution in pixels/meter (informational only).
    /// </summary>
    public int XResolution { get; set; }

    /// <summary>
    /// Vertical resolution in pixels/meter (informational only).
    /// </summary>
    public int YResolution { get; set; }

    /// <summary>
    /// Default pixel value for the page buffer (0 = white, 1 = black).
    /// </summary>
    public byte DefaultPixelValue { get; set; }

    /// <summary>
    /// Combination operator used when compositing regions onto the page buffer.
    /// </summary>
    public Jbig2CombinationOperator CombinationOperator { get; set; }

    /// <summary>
    /// Whether the page requires a page buffer (striped mode when false).
    /// </summary>
    public bool RequiresBuffer { get; set; } = true;
}
