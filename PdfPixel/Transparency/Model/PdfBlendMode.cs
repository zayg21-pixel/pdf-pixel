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

    /// <summary>
    /// PDF blend mode name constants and utilities
    /// </summary>
    public static class PdfBlendModeNames
    {
        /// <summary>
        /// Convert PDF blend mode to SkiaSharp blend mode
        /// Note: Some PDF blend modes may not have exact SkiaSharp equivalents
        /// </summary>
        /// <param name="pdfBlendMode">PDF blend mode</param>
        /// <returns>Corresponding SkiaSharp blend mode</returns>
        public static SKBlendMode ToSkiaBlendMode(PdfBlendMode pdfBlendMode)
        {
            return pdfBlendMode switch
            {
                PdfBlendMode.Normal => SKBlendMode.SrcOver,
                PdfBlendMode.Multiply => SKBlendMode.Multiply,
                PdfBlendMode.Screen => SKBlendMode.Screen,
                PdfBlendMode.Overlay => SKBlendMode.Overlay,
                PdfBlendMode.SoftLight => SKBlendMode.SoftLight,
                PdfBlendMode.HardLight => SKBlendMode.HardLight,
                PdfBlendMode.ColorDodge => SKBlendMode.ColorDodge,
                PdfBlendMode.ColorBurn => SKBlendMode.ColorBurn,
                PdfBlendMode.Darken => SKBlendMode.Darken,
                PdfBlendMode.Lighten => SKBlendMode.Lighten,
                PdfBlendMode.Difference => SKBlendMode.Difference,
                PdfBlendMode.Exclusion => SKBlendMode.Exclusion,
                PdfBlendMode.Hue => SKBlendMode.Hue,
                PdfBlendMode.Saturation => SKBlendMode.Saturation,
                PdfBlendMode.Color => SKBlendMode.Color,
                PdfBlendMode.Luminosity => SKBlendMode.Luminosity,
                PdfBlendMode.Compatible => SKBlendMode.SrcOver, // Default to normal
                _ => SKBlendMode.SrcOver // Default fallback
            };
        }
    }
}