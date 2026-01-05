namespace PdfReader.Imaging.Jpx.Model;

/// <summary>
/// Represents a single image component in a JPEG 2000 image (from SIZ marker segment).
/// </summary>
internal sealed class JpxComponent
{
    /// <summary>
    /// Gets or sets the component sample precision in bits (Ssiz parameter).
    /// Bits 0-6: precision (1-38), Bit 7: signed flag (0=unsigned, 1=signed).
    /// </summary>
    public byte SamplePrecision { get; set; }

    /// <summary>
    /// Gets or sets the horizontal separation (XRsiz parameter).
    /// Component sub-sampling factor relative to the reference grid.
    /// </summary>
    public byte HorizontalSeparation { get; set; }

    /// <summary>
    /// Gets or sets the vertical separation (YRsiz parameter).
    /// Component sub-sampling factor relative to the reference grid.
    /// </summary>
    public byte VerticalSeparation { get; set; }

    /// <summary>
    /// Gets a value indicating whether this component uses signed samples.
    /// </summary>
    public bool IsSigned => (SamplePrecision & 0x80) != 0;

    /// <summary>
    /// Gets the precision in bits (1-38).
    /// </summary>
    public int PrecisionBits => (SamplePrecision & 0x7F) + 1;
}