using SkiaSharp;

namespace PdfPixel.Transparency.Model;

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
        if (pdfBlendMode == PdfBlendMode.Multiply)
        {

        }

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
