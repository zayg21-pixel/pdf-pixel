namespace PdfPixel.Imaging.Jpg.Model;

/// <summary>
/// Minimal component descriptor as declared in the JPEG SOF segment.
/// </summary>
internal sealed class JpgComponent
{
    /// <summary>
    /// Gets or sets the JPEG component identifier.
    /// </summary>
    public byte Id { get; set; }

    /// <summary>
    /// Gets or sets the horizontal sampling factor for this component.
    /// </summary>
    public byte HorizontalSamplingFactor { get; set; }

    /// <summary>
    /// Gets or sets the vertical sampling factor for this component.
    /// </summary>
    public byte VerticalSamplingFactor { get; set; }

    /// <summary>
    /// Gets or sets the quantization table identifier for this component.
    /// </summary>
    public byte QuantizationTableId { get; set; }
}
