using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Transform;
using System;
using System.Numerics;

namespace PdfPixel.Color.Icc;

/// <summary>
/// Provides black point measurements for black point compensation.
/// </summary>
internal static class IccBlackPoint
{
    private const float BlackPointMaxLightness = 50f;
    private const float NegativeProfileMinLightness = 95f;
    private const float NeutralLabEncoded = 128f / 255f;

    /// <summary>
    /// Gets the black point of the perceptual reference medium in CIE XYZ (D50).
    /// </summary>
    public static Vector4 PerceptualBlack { get; } = new(0.00336f, 0.0034731f, 0.00287f, 1f);

    /// <summary>
    /// Measures the black point as the color of the darkest colorant in CIE XYZ (D50).
    /// </summary>
    /// <param name="header">The ICC profile header.</param>
    /// <param name="deviceToPcs">The device to profile connection space transform.</param>
    /// <param name="pcsToXyz">The profile connection space to CIE XYZ transform, or null when no conversion is needed.</param>
    /// <returns>The black point, or null when the color space has no darkest colorant.</returns>
    public static Vector4? GetDarkestColorantBlackPoint(IccProfileHeader header, IColorTransform deviceToPcs, IColorTransform? pcsToXyz)
    {
        Vector4 darkestColorant;
        switch (header.ColorSpace)
        {
            case IccColorSpace.Gray:
            case IccColorSpace.Rgb:
                {
                    darkestColorant = Vector4.Zero;
                    break;
                }
            case IccColorSpace.Lab:
                {
                    darkestColorant = new Vector4(0f, NeutralLabEncoded, NeutralLabEncoded, 0f);
                    break;
                }
            case IccColorSpace.Cmyk:
                {
                    darkestColorant = Vector4.One;
                    break;
                }
            case IccColorSpace.Cmy:
                {
                    darkestColorant = new Vector4(1f, 1f, 1f, 0f);
                    break;
                }
            default:
                {
                    return null;
                }
        }

        Vector3 lab = DeviceToLab(header, deviceToPcs, pcsToXyz, darkestColorant);

        // Non-compliant negative profiles report white as the darkest colorant.
        if (lab.X > NegativeProfileMinLightness || lab.X < 0f)
        {
            lab.X = 0f;
        }
        else if (lab.X > BlackPointMaxLightness)
        {
            lab.X = BlackPointMaxLightness;
        }

        return IccTransforms.LabToXyz(lab);
    }

    /// <summary>
    /// Measures the black point by round-tripping the PCS black through the perceptual output table in CIE XYZ (D50).
    /// </summary>
    /// <param name="header">The ICC profile header.</param>
    /// <param name="perceptualOutput">The perceptual PCS to device table.</param>
    /// <param name="deviceToPcs">The device to profile connection space transform.</param>
    /// <param name="pcsToXyz">The profile connection space to CIE XYZ transform, or null when no conversion is needed.</param>
    /// <returns>The black point.</returns>
    public static Vector4 GetPerceptualBlackPoint(IccProfileHeader header, IccLutPipeline perceptualOutput, IColorTransform deviceToPcs, IColorTransform? pcsToXyz)
    {
        Vector4 pcsBlack = Vector4.Zero;
        if (header.Pcs == IccColorSpace.Lab)
        {
            pcsBlack = new Vector4(0f, NeutralLabEncoded, NeutralLabEncoded, 0f);
        }

        Vector4 device = perceptualOutput.Transform.Transform(pcsBlack);
        Vector3 lab = DeviceToLab(header, deviceToPcs, pcsToXyz, device);

        Vector3 neutralLab = new(Math.Min(lab.X, BlackPointMaxLightness), 0f, 0f);

        return IccTransforms.LabToXyz(neutralLab);
    }

    /// <summary>
    /// Determines whether the profile color space is an ink color space.
    /// </summary>
    /// <param name="header">The ICC profile header.</param>
    /// <returns><c>true</c> if the color space is CMY, CMYK or multichannel; otherwise, <c>false</c>.</returns>
    public static bool IsInkColorSpace(IccProfileHeader header)
    {
        IccColorSpace colorSpace = header.ColorSpace;
        if (colorSpace == IccColorSpace.Cmyk || colorSpace == IccColorSpace.Cmy)
        {
            return true;
        }

        string? colorSpaceName = header.ColorSpaceName;
        if (colorSpaceName == null)
        {
            return false;
        }

        return colorSpaceName.StartsWith("MCH", StringComparison.Ordinal)
            || colorSpaceName.EndsWith("CLR", StringComparison.Ordinal);
    }

    private static Vector3 DeviceToLab(IccProfileHeader header, IColorTransform deviceToPcs, IColorTransform? pcsToXyz, Vector4 color)
    {
        Vector4 pcs = deviceToPcs.Transform(color);

        if (header.Pcs == IccColorSpace.Lab)
        {
            return new Vector3(pcs.X * 100f, (pcs.Y * 255f) - 128f, (pcs.Z * 255f) - 128f);
        }

        if (pcsToXyz != null)
        {
            pcs = pcsToXyz.Transform(pcs);
        }

        return IccTransforms.XyzToLab(pcs);
    }
}
