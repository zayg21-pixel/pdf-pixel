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

        PdfColorConversionParameters parameters = new(PdfRenderingIntent.RelativeColorimetric, null, true, page.Document.ContentHost.Resources.DeviceColorMapping);

        PdfColorSpaceConverter? converter = PdfColorSpaceResolver.ResolveDeviceConverter(colorComponents.Length);
        if (converter == null)
        {
            converter = PdfColorSpaceResolver.ResolveDeviceConverter(3) ?? PdfDeviceRgbColorSpaceConverter.Instance;
            float[] paddedColor = colorComponents;
            Array.Resize(ref paddedColor, 3);
            return converter.ToSrgb(paddedColor, parameters);
        }

        return converter.ToSrgb(colorComponents, parameters);
    }
}
