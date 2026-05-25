namespace PdfPixel.Jpx.Model;

/// <summary>
/// Represents component-specific quantization parameters from QCC marker segment.
/// </summary>
public sealed class JpxComponentQuantization
{
    internal JpxComponentQuantization(ushort componentIndex, JpxQuantization quantization)
    {
        ComponentIndex = componentIndex;
        Quantization = quantization;
    }

    /// <summary>
    /// Gets or sets the component index this quantization applies to.
    /// </summary>
    public ushort ComponentIndex { get; }

    /// <summary>
    /// Gets or sets the quantization parameters for this component.
    /// </summary>
    public JpxQuantization Quantization { get; }
}
