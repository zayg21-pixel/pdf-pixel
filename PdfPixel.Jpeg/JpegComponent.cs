namespace PdfPixel.Jpeg;

/// <summary>
/// Minimal component descriptor as declared in the JPEG SOF segment.
/// </summary>
public sealed class JpegComponent
{
    /// <summary>
    /// Gets or sets the JPEG component identifier.
    /// </summary>
    public byte Id { get; internal set; }

    /// <summary>
    /// Gets or sets the horizontal sampling factor for this component.
    /// </summary>
    public byte HorizontalSamplingFactor { get; internal set; }

    /// <summary>
    /// Gets or sets the vertical sampling factor for this component.
    /// </summary>
    public byte VerticalSamplingFactor { get; internal set; }

    /// <summary>
    /// Gets or sets the quantization table identifier for this component.
    /// </summary>
    public byte QuantizationTableId { get; internal set; }
}
