using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Transform;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc;

/// <summary>
/// Provides color transformation functionality using ICC profiles for color space conversion.
/// </summary>
public sealed class IccProfileTransform
{
    private const float BlackPointMaxLightness = 50f;
    private const float NegativeProfileMinLightness = 95f;
    private const float NeutralLabEncoded = 128f / 255f;
    private const float CieEpsilon = 216f / 24389f;
    private const float CieKappa = 24389f / 27f;

    private static readonly Vector4 PerceptualBlack = new(0.00336f, 0.0034731f, 0.00287f, 1f);

    private readonly bool _hasLut;
    private readonly IColorTransform _transform;
    private readonly IColorTransform? _pcsToXyzTransform;
    private readonly IccProfile _iccProfile;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfileTransform"/> class using the specified ICC profile.
    /// </summary>
    /// <param name="profile">The ICC profile to use for color transformation.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="profile"/> is null.</exception>
    public IccProfileTransform(IccProfile profile)
    {
        _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));

        IccLutPipeline? lut = GetA2BLutByIntent(profile);

        if (lut != null)
        {
            _hasLut = true;
            _transform = new ChainedColorTransform();
        }
        else if (profile.GrayTrc != null && profile.Header.Pcs == IccColorSpace.Lab)
        {
            // Neutral a*/b* in the ICC 8-bit Lab encoding: 0.5 == 0.
            _transform = new ChainedColorTransform(
                new PerChannelTrcTransform([profile.GrayTrc]),
                new FunctionColorTransform(x => new Vector4(x.X, 0.5f, 0.5f, 1f)));
        }
        else if (profile.GrayTrc != null)
        {
            _transform = new ChainedColorTransform(
                new PerChannelTrcTransform([profile.GrayTrc]),
                new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1)));
        }
        else if (profile.Header.ColorSpace == IccColorSpace.Lab)
        {
            _transform = IccTransforms.LabD50ToXyzTransform;
        }
        else if (profile.RedMatrix.HasValue && profile.GreenMatrix.HasValue && profile.BlueMatrix.HasValue)
        {
            List<IColorTransform> matrixTransforms = [];

            if (profile.RedTrc != null && profile.GreenTrc != null && profile.BlueTrc != null)
            {
                PerChannelTrcTransform trcLuts = new([profile.RedTrc, profile.GreenTrc, profile.BlueTrc]);
                matrixTransforms.Add(trcLuts);
            }

            MatrixColorTransform matrixTransform = new([profile.RedMatrix.Value, profile.GreenMatrix.Value, profile.BlueMatrix.Value]);
            matrixTransforms.Add(matrixTransform);

            _transform = new ChainedColorTransform(matrixTransforms.ToArray());
        }
        else
        {
            _transform = new ChainedColorTransform();
        }

        if (profile.Header.Pcs == IccColorSpace.Lab)
        {
            _pcsToXyzTransform = IccTransforms.LabD50ToXyzTransform;
        }
        else if (_hasLut)
        {
            _pcsToXyzTransform = new FunctionColorTransform(x => x * 2.0f);
        }
    }

    /// <summary>
    /// Gets a chained color transform for the specified PDF rendering intent.
    /// </summary>
    /// <param name="intent">The PDF rendering intent to use for the transformation. In null - default intent from ICC header will be used.</param>
    /// <param name="useBlackPointCompensation">Whether black point compensation is applied.</param>
    /// <returns>A <see cref="ChainedColorTransform"/> representing the color transformation pipeline.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChainedColorTransform GetIntentTransform(IccRenderingIntent? intent = default, bool useBlackPointCompensation = true)
    {
        IccRenderingIntent resolvedIntent = intent ?? _iccProfile.Header.RenderingIntent;
        IccLutPipeline? lutTransform = GetA2BLutByIntent(_iccProfile, resolvedIntent);

        FunctionColorTransform? blackPointCompensation = null;
        if (useBlackPointCompensation)
        {
            Vector4? blackPoint = GetBlackPoint(resolvedIntent);
            if (blackPoint != null)
            {
                blackPointCompensation = CreateBlackPointCompensation(blackPoint.Value);
            }
        }

        return new ChainedColorTransform(
            (_hasLut && lutTransform != null) ? lutTransform.Transform : _transform,
            _pcsToXyzTransform,
            blackPointCompensation,
            IccTransforms.XyzD50ToSrgbTransform);
    }

    private Vector4? GetBlackPoint(IccRenderingIntent intent)
    {
        if (intent == IccRenderingIntent.AbsoluteColorimetric)
        {
            return null;
        }

        string? deviceClass = _iccProfile.Header.DeviceClass;
        if (deviceClass == "link" || deviceClass == "abst" || deviceClass == "nmcl")
        {
            return null;
        }

        Vector4? blackPoint;
        Version? version = _iccProfile.Header.Version;
        bool isPerceptualOrSaturation = (intent == IccRenderingIntent.Perceptual) || (intent == IccRenderingIntent.Saturation);

        if (version != null && version.Major >= 4 && isPerceptualOrSaturation && HasTrcModel())
        {
            blackPoint = GetDarkestColorantBlackPoint(IccRenderingIntent.RelativeColorimetric);
        }
        else if (version != null && version.Major >= 4 && isPerceptualOrSaturation)
        {
            blackPoint = PerceptualBlack;
        }
        else if (intent == IccRenderingIntent.RelativeColorimetric && deviceClass == "prtr" && IsInkColorSpace())
        {
            blackPoint = GetPerceptualBlackPoint();
        }
        else
        {
            blackPoint = GetDarkestColorantBlackPoint(intent);
        }

        if (blackPoint == null)
        {
            return null;
        }

        if (blackPoint.Value.X == 0f
            && blackPoint.Value.Y == 0f
            && blackPoint.Value.Z == 0f)
        {
            return null;
        }

        return blackPoint;
    }

    private Vector4? GetDarkestColorantBlackPoint(IccRenderingIntent intent)
    {
        if (!IsIntentSupported(intent))
        {
            return null;
        }

        Vector4 darkestColorant;
        switch (_iccProfile.Header.ColorSpace)
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

        Vector3 lab = DeviceToLab(darkestColorant, intent);

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

    private Vector4? GetPerceptualBlackPoint()
    {
        IccLutPipeline? perceptualOutput = _iccProfile.B2ALut0;
        if (!IsIntentSupported(IccRenderingIntent.Perceptual) || perceptualOutput == null)
        {
            return null;
        }

        Vector4 pcsBlack = Vector4.Zero;
        if (_iccProfile.Header.Pcs == IccColorSpace.Lab)
        {
            pcsBlack = new Vector4(0f, NeutralLabEncoded, NeutralLabEncoded, 0f);
        }

        Vector4 device = perceptualOutput.Transform.Transform(pcsBlack);
        Vector3 lab = DeviceToLab(device, IccRenderingIntent.RelativeColorimetric);

        Vector3 neutralLab = new(Math.Min(lab.X, BlackPointMaxLightness), 0f, 0f);

        return LabToXyz(neutralLab);
    }

    private Vector3 DeviceToLab(Vector4 color, IccRenderingIntent intent)
    {
        IccLutPipeline? lut = GetA2BLutByIntent(_iccProfile, intent);

        Vector4 pcs;
        if (_hasLut && lut != null)
        {
            pcs = lut.Transform.Transform(color);
        }
        else
        {
            pcs = _transform.Transform(color);
        }

        if (_iccProfile.Header.Pcs == IccColorSpace.Lab)
        {
            return new Vector3(pcs.X * 100f, (pcs.Y * 255f) - 128f, (pcs.Z * 255f) - 128f);
        }

        if (_pcsToXyzTransform != null)
        {
            pcs = _pcsToXyzTransform.Transform(pcs);
        }

        return XyzToLab(pcs);
    }

    private bool IsIntentSupported(IccRenderingIntent intent)
    {
        switch (intent)
        {
            case IccRenderingIntent.Perceptual:
                {
                    return _iccProfile.A2BLut0 != null || HasTrcModel();
                }
            case IccRenderingIntent.RelativeColorimetric:
            case IccRenderingIntent.AbsoluteColorimetric:
                {
                    return _iccProfile.A2BLut1 != null || HasTrcModel();
                }
            case IccRenderingIntent.Saturation:
                {
                    return _iccProfile.A2BLut2 != null || HasTrcModel();
                }
            default:
                {
                    return false;
                }
        }
    }

    private bool HasTrcModel()
    {
        switch (_iccProfile.Header.ColorSpace)
        {
            case IccColorSpace.Gray:
                {
                    return _iccProfile.GrayTrc != null;
                }
            case IccColorSpace.Rgb:
                {
                    return _iccProfile.RedMatrix != null
                        && _iccProfile.GreenMatrix != null
                        && _iccProfile.BlueMatrix != null
                        && _iccProfile.RedTrc != null
                        && _iccProfile.GreenTrc != null
                        && _iccProfile.BlueTrc != null;
                }
            default:
                {
                    return false;
                }
        }
    }

    private bool IsInkColorSpace()
    {
        IccColorSpace colorSpace = _iccProfile.Header.ColorSpace;
        if (colorSpace == IccColorSpace.Cmyk || colorSpace == IccColorSpace.Cmy)
        {
            return true;
        }

        string? colorSpaceName = _iccProfile.Header.ColorSpaceName;
        if (colorSpaceName == null)
        {
            return false;
        }

        return colorSpaceName.StartsWith("MCH", StringComparison.Ordinal)
            || colorSpaceName.EndsWith("CLR", StringComparison.Ordinal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IccLutPipeline? GetA2BLutByIntent(IccProfile profile, IccRenderingIntent? intentOverride = default)
    {
        IccRenderingIntent intent = intentOverride ?? profile.Header.RenderingIntent;

        switch (intent)
        {
            case IccRenderingIntent.Perceptual:
                return profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;

            case IccRenderingIntent.RelativeColorimetric:
                return profile.A2BLut1 ?? profile.A2BLut0 ?? profile.A2BLut2;

            case IccRenderingIntent.Saturation:
                return profile.A2BLut2 ?? profile.A2BLut0 ?? profile.A2BLut1;

            case IccRenderingIntent.AbsoluteColorimetric:
                return profile.A2BLut1 ?? profile.A2BLut0 ?? profile.A2BLut2;

            default:
                return profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;
        }
    }

    private static FunctionColorTransform CreateBlackPointCompensation(Vector4 blackPoint)
    {
        Vector4 white = IccTransforms.D50WhitePoint;
        Vector4 scale = new(
            white.X / (white.X - blackPoint.X),
            white.Y / (white.Y - blackPoint.Y),
            white.Z / (white.Z - blackPoint.Z),
            1f);
        Vector4 offset = new(
            -blackPoint.X * scale.X,
            -blackPoint.Y * scale.Y,
            -blackPoint.Z * scale.Z,
            0f);

        return new FunctionColorTransform(x => (x * scale) + offset);
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
