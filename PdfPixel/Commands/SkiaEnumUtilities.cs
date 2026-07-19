using PdfPixel.Models;
using PdfPixel.Transparency.Model;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Converts PDF model enums to their SkiaSharp equivalents.
/// </summary>
internal static class SkiaEnumUtilities
{
    /// <summary>
    /// Converts a <see cref="PdfClipOperation"/> to the corresponding <see cref="SKClipOperation"/>.
    /// </summary>
    public static SKClipOperation ToSkClipOperation(PdfClipOperation operation)
    {
        return operation switch
        {
            PdfClipOperation.Intersect => SKClipOperation.Intersect,
            PdfClipOperation.Difference => SKClipOperation.Difference,
            _ => SKClipOperation.Intersect
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfBlendMode"/> to the corresponding <see cref="SKBlendMode"/>.
    /// Note: Some PDF blend modes may not have exact SkiaSharp equivalents.
    /// </summary>
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
