using PdfPixel.Text;

namespace PdfPixel.Transparency.Model;

/// <summary>
/// PDF blend modes enumeration
/// Represents the blend modes supported by PDF documents for transparency operations
/// Based on PDF Reference Section 7.2.4
/// </summary>
[PdfEnum]
public enum PdfBlendMode
{
    /// <summary>
    /// Unrecognized or absent blend mode name.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// The result is the source color, unmodified by the backdrop.
    /// </summary>
    [PdfEnumValue("Normal")]
    Normal,

    /// <summary>
    /// Multiplies source and backdrop component values, always producing a darker result.
    /// </summary>
    [PdfEnumValue("Multiply")]
    Multiply,

    /// <summary>
    /// Complements and multiplies the inverses of source and backdrop, always producing a lighter result.
    /// </summary>
    [PdfEnumValue("Screen")]
    Screen,

    /// <summary>
    /// Multiplies or screens depending on the backdrop value; preserves highlights and shadows.
    /// </summary>
    [PdfEnumValue("Overlay")]
    Overlay,

    /// <summary>
    /// Darkens or lightens depending on the source color, with a softer effect than Hard Light.
    /// </summary>
    [PdfEnumValue("SoftLight")]
    SoftLight,

    /// <summary>
    /// Multiplies or screens depending on the source color, with a harder effect than Soft Light.
    /// </summary>
    [PdfEnumValue("HardLight")]
    HardLight,

    /// <summary>
    /// Brightens the backdrop to reflect the source color; black source produces no change.
    /// </summary>
    [PdfEnumValue("ColorDodge")]
    ColorDodge,

    /// <summary>
    /// Darkens the backdrop to reflect the source color; white source produces no change.
    /// </summary>
    [PdfEnumValue("ColorBurn")]
    ColorBurn,

    /// <summary>
    /// Retains the darker of source and backdrop for each color component.
    /// </summary>
    [PdfEnumValue("Darken")]
    Darken,

    /// <summary>
    /// Retains the lighter of source and backdrop for each color component.
    /// </summary>
    [PdfEnumValue("Lighten")]
    Lighten,

    /// <summary>
    /// Subtracts the darker component value from the lighter, producing an inversion effect.
    /// </summary>
    [PdfEnumValue("Difference")]
    Difference,

    /// <summary>
    /// Similar to Difference but with lower contrast around mid-tones.
    /// </summary>
    [PdfEnumValue("Exclusion")]
    Exclusion,

    /// <summary>
    /// Applies the hue of the source with the saturation and luminosity of the backdrop.
    /// </summary>
    [PdfEnumValue("Hue")]
    Hue,

    /// <summary>
    /// Applies the saturation of the source with the hue and luminosity of the backdrop.
    /// </summary>
    [PdfEnumValue("Saturation")]
    Saturation,

    /// <summary>
    /// Applies the hue and saturation of the source with the luminosity of the backdrop.
    /// </summary>
    [PdfEnumValue("Color")]
    Color,

    /// <summary>
    /// Applies the luminosity of the source with the hue and saturation of the backdrop.
    /// </summary>
    [PdfEnumValue("Luminosity")]
    Luminosity,

    /// <summary>
    /// Legacy compatibility mode; treated as Normal outside transparency groups.
    /// </summary>
    [PdfEnumValue("Compatible")]
    Compatible
}
