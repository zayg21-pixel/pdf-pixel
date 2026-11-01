using PdfPixel.Color.Transform;
using System;
using System.Numerics;

namespace PdfPixel.Color.Icc;

/// <summary>
/// Provides black point measurements and black point compensation.
/// </summary>
public static class IccBlackPoint
{
    private const float CieEpsilon = 216f / 24389f;
    private const float CieKappa = 24389f / 27f;
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
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (deviceToPcs == null)
        {
            throw new ArgumentNullException(nameof(deviceToPcs));
        }

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

        return LabToXyz(lab);
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
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (perceptualOutput == null)
        {
            throw new ArgumentNullException(nameof(perceptualOutput));
        }

        if (deviceToPcs == null)
        {
            throw new ArgumentNullException(nameof(deviceToPcs));
        }

        Vector4 pcsBlack = Vector4.Zero;
        if (header.Pcs == IccColorSpace.Lab)
        {
            pcsBlack = new Vector4(0f, NeutralLabEncoded, NeutralLabEncoded, 0f);
        }

        Vector4 device = perceptualOutput.Transform.Transform(pcsBlack);
        Vector3 lab = DeviceToLab(header, deviceToPcs, pcsToXyz, device);

        return ToNeutralBlackPoint(lab);
    }

    /// <summary>
    /// Measures the black point as the neutral color at the lightness of a known black point in CIE XYZ (D50).
    /// </summary>
    /// <param name="blackPoint">The known black point in CIE XYZ (D50).</param>
    /// <returns>The black point.</returns>
    public static Vector4 GetNeutralBlackPoint(Vector4 blackPoint) => ToNeutralBlackPoint(XyzToLab(blackPoint));

    /// <summary>
    /// Builds a black point compensation transform that maps the specified black point to zero in CIE XYZ (D50).
    /// </summary>
    /// <param name="blackPoint">The source black point in CIE XYZ (D50).</param>
    /// <returns>An <see cref="IColorTransform"/> that applies black point compensation, or null when the black point is already zero.</returns>
    public static IColorTransform? BuildBlackPointCompensationTransform(Vector4 blackPoint)
    {
        Vector4 black = new(blackPoint.X, blackPoint.Y, blackPoint.Z, 0f);
        if (black == Vector4.Zero)
        {
            return null;
        }

        Vector4 white = IccTransforms.D50WhitePoint;
        Vector4 scale = white / (white - black);
        Vector4 offset = -black * scale;

        return new FunctionColorTransform(x => (x * scale) + offset);
    }

    /// <summary>
    /// Determines whether the profile color space is an ink color space.
    /// </summary>
    /// <param name="header">The ICC profile header.</param>
    /// <returns><c>true</c> if the color space is CMY, CMYK or multichannel; otherwise, <c>false</c>.</returns>
    public static bool IsInkColorSpace(IccProfileHeader header)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

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

    private static Vector4 ToNeutralBlackPoint(Vector3 lab) => LabToXyz(new Vector3(Math.Min(lab.X, BlackPointMaxLightness), 0f, 0f));

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

        return XyzToLab(pcs);
    }

    private static Vector3 XyzToLab(Vector4 xyz)
    {
        Vector4 white = IccTransforms.D50WhitePoint;
        float fx = LabForward(xyz.X / white.X);
        float fy = LabForward(xyz.Y / white.Y);
        float fz = LabForward(xyz.Z / white.Z);

        return new Vector3((116f * fy) - 16f, 500f * (fx - fy), 200f * (fy - fz));
    }

    private static Vector4 LabToXyz(Vector3 lab)
    {
        Vector4 white = IccTransforms.D50WhitePoint;
        float fy = (lab.X + 16f) / 116f;
        float fx = fy + (lab.Y / 500f);
        float fz = fy - (lab.Z / 200f);

        return new Vector4(LabInverse(fx) * white.X, LabInverse(fy) * white.Y, LabInverse(fz) * white.Z, 1f);
    }

    private static float LabForward(float ratio)
    {
        if (ratio > CieEpsilon)
        {
            return (float)Math.Pow(ratio, 1.0 / 3.0);
        }

        return ((CieKappa * ratio) + 16f) / 116f;
    }

    private static float LabInverse(float value)
    {
        float cube = value * value * value;
        if (cube > CieEpsilon)
        {
            return cube;
        }

        return ((116f * value) - 16f) / CieKappa;
    }
}
