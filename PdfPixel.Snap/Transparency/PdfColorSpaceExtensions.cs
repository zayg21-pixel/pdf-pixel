using PdfPixel.Color.ColorSpace;
using System;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// Compositing properties of a color space used as a blending color space (ISO 32000-2 11.3.4).
/// </summary>
internal static class PdfColorSpaceExtensions
{
    /// <summary>
    /// Returns the process color model of <paramref name="colorSpace"/> by its component count: 1 gray, 4 CMYK, any
    /// other RGB.
    /// </summary>
    public static SnapBlendingColorModel GetBlendingColorModel(this PdfColorSpaceConverter colorSpace)
    {
        switch (colorSpace.Components)
        {
            case 1:
            {
                return SnapBlendingColorModel.Gray;
            }
            case 4:
            {
                return SnapBlendingColorModel.Cmyk;
            }
            default:
            {
                return SnapBlendingColorModel.Rgb;
            }
        }
    }

    /// <summary>
    /// Returns the luminosity of a color for a luminosity soft mask, by the device-space formulas of 11.5.3.
    /// </summary>
    public static float GetLuminosity(this PdfColorSpaceConverter colorSpace, in ReadOnlySpan<float> components)
    {
        switch (colorSpace.GetBlendingColorModel())
        {
            case SnapBlendingColorModel.Gray:
            {
                return components[0];
            }
            case SnapBlendingColorModel.Rgb:
            {
                return (0.30f * components[0]) + (0.59f * components[1]) + (0.11f * components[2]);
            }
            default:
            {
                float coverage = (0.3f * components[0]) + (0.59f * components[1]) + (0.11f * components[2]) + components[3];

                return 1 - Math.Min(1, coverage);
            }
        }
    }

    /// <summary>
    /// Returns <paramref name="colorSpace"/> when 11.3.4 allows it as a blending color space, otherwise
    /// <see langword="null"/>.
    /// </summary>
    public static PdfColorSpaceConverter? GetBlendingColorSpace(this PdfColorSpaceConverter colorSpace)
    {
        switch (colorSpace)
        {
            case PdfDeviceGrayColorSpaceConverter:
            case PdfDeviceRgbColorSpaceConverter:
            case PdfDeviceCmykColorSpaceConverter:
            case PdfCalibratedColorSpaceConverter:
            case PdfIccColorSpaceConverter:
            {
                return colorSpace;
            }
            default:
            {
                return null;
            }
        }
    }
}
