using PdfPixel.Models;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using System;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Provides utilities for resolving annotation colors with proper color space conversion.
/// </summary>
internal static class PdfAnnotationColorResolver
{
    /// <summary>
    /// Resolves annotation color using proper color space conversion.
    /// </summary>
    /// <param name="colorComponents">The color component array from the annotation.</param>
    /// <param name="page">The PDF page for color space resolution.</param>
    /// <param name="defaultColor">Default color to use if annotation has no color specified. If null, returns transparent.</param>
    /// <returns>The resolved color for rendering.</returns>
    public static PdfColor ResolveColor(float[]? colorComponents, IPdfPageInternal page, PdfColor? defaultColor = null)
    {
        if (colorComponents == null || colorComponents.Length == 0)
        {
            return defaultColor ?? PdfColors.Transparent;
        }

        PdfColorSpaceConverter? converter = page.Cache.ColorSpace.ResolveDeviceConverter(colorComponents.Length);
        if (converter == null)
        {
            converter = page.Cache.ColorSpace.ResolveDeviceConverter(3) ?? PdfDeviceRgbColorSpaceConverter.Instance;
            float[] paddedColor = colorComponents;
            Array.Resize(ref paddedColor, 3);
            return converter.ToSrgb(paddedColor, PdfRenderingIntent.RelativeColorimetric, null);
        }

        return converter.ToSrgb(colorComponents, PdfRenderingIntent.RelativeColorimetric, null);
    }
}
