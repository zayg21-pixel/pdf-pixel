using PdfPixel.Models;
using PdfPixel.Color.ColorSpace;
using SkiaSharp;
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
    /// <returns>The resolved SKColor for rendering.</returns>
    public static SKColor ResolveColor(float[] colorComponents, PdfPage page, SKColor? defaultColor = null)
    {
        if (colorComponents == null || colorComponents.Length == 0)
        {
            return defaultColor ?? SKColors.Transparent;
        }

        var converter = page.Cache.ColorSpace.ResolveDeviceConverter(colorComponents.Length);
        if (converter == null)
        {
            converter = page.Cache.ColorSpace.ResolveDeviceConverter(3);
            var paddedColor = colorComponents;
            Array.Resize(ref paddedColor, 3);
            return converter.ToSrgb(paddedColor, PdfRenderingIntent.RelativeColorimetric, null);
        }

        return converter.ToSrgb(colorComponents, PdfRenderingIntent.RelativeColorimetric, null);
    }
}
