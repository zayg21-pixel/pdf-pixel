using SkiaSharp;
using PdfPixel.Text;

namespace PdfPixel.Transparency.Model
{
    /// <summary>
    /// PDF blend modes enumeration
    /// Represents the blend modes supported by PDF documents for transparency operations
    /// Based on PDF Reference Section 7.2.4
    /// </summary>
    [PdfEnum]
    public enum PdfBlendMode
    {
        [PdfEnumDefaultValue]
        Unknown,

        [PdfEnumValue("Normal")]
        Normal,

        [PdfEnumValue("Multiply")]
        Multiply,

        [PdfEnumValue("Screen")]
        Screen,

        [PdfEnumValue("Overlay")]
        Overlay,

        [PdfEnumValue("SoftLight")]
        SoftLight,

        [PdfEnumValue("HardLight")]
        HardLight,

        [PdfEnumValue("ColorDodge")]
        ColorDodge,

        [PdfEnumValue("ColorBurn")]
        ColorBurn,

        [PdfEnumValue("Darken")]
        Darken,

        [PdfEnumValue("Lighten")]
        Lighten,

        [PdfEnumValue("Difference")]
        Difference,

        [PdfEnumValue("Exclusion")]
        Exclusion,

        [PdfEnumValue("Hue")]
        Hue,

        [PdfEnumValue("Saturation")]
        Saturation,

        [PdfEnumValue("Color")]
        Color,

        [PdfEnumValue("Luminosity")]
        Luminosity,

        [PdfEnumValue("Compatible")]
        Compatible
    }
}
