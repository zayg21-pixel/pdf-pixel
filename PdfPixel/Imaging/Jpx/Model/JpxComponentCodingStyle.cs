namespace PdfPixel.Imaging.Jpx.Model;

/// <summary>
/// Represents component-specific coding style parameters from COC marker segment.
/// </summary>
internal sealed class JpxComponentCodingStyle
{
    /// <summary>
    /// Gets or sets the component index this coding style applies to.
    /// </summary>
    public ushort ComponentIndex { get; set; }

    /// <summary>
    /// Gets or sets the coding style parameters for this component.
    /// </summary>
    public JpxCodingStyle CodingStyle { get; set; }
}