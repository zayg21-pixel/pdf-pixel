namespace PdfPixel.Jpx.Model;

/// <summary>
/// Represents component-specific quantization parameters from QCC marker segment.
/// </summary>
public sealed class JpxComponentQuantization
{
    /// <summary>
    /// Gets or sets the component index this quantization applies to.
    /// </summary>
    public ushort ComponentIndex { get; set; }

    /// <summary>
    /// Gets or sets the quantization parameters for this component.
    /// </summary>
    public JpxQuantization Quantization { get; set; }
}