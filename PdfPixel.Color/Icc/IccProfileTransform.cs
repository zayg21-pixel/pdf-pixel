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
    private const int IntentCount = 4;

    private readonly IccLutPipeline?[] _intentLuts = new IccLutPipeline?[IntentCount];
    private readonly IColorTransform?[] _blackPointCompensationTransforms = new IColorTransform?[IntentCount];
    private readonly IColorTransform _modelTransform;
    private readonly IColorTransform? _lutPcsToXyzTransform;
    private readonly IColorTransform? _modelPcsToXyzTransform;
    private readonly IccProfile _iccProfile;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfileTransform"/> class using the specified ICC profile.
    /// </summary>
    /// <param name="profile">The ICC profile to use for color transformation.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="profile"/> is null.</exception>
    public IccProfileTransform(IccProfile profile)
    {
        _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));

        var hasTrcModel = false;
        var hasModel = true;

        if (profile.Header.Pcs == IccColorSpace.Lab)
        {
            _lutPcsToXyzTransform = IccTransforms.LabD50ToXyzTransform;
            _modelPcsToXyzTransform = IccTransforms.LabD50ToXyzTransform;
        }
        else
        {
            _lutPcsToXyzTransform = new FunctionColorTransform(x => x * 2.0f);
        }

        if (profile.GrayTrc != null)
        {
            if (profile.Header.Pcs == IccColorSpace.Lab)
            {
                // Neutral a*/b* in the ICC 8-bit Lab encoding: 0.5 == 0.
                _modelTransform = new ChainedColorTransform(
                    new PerChannelTrcTransform([profile.GrayTrc]),
                    new FunctionColorTransform(x => new Vector4(x.X, 0.5f, 0.5f, 1f)));
            }
            else
            {
                _modelTransform = new ChainedColorTransform(
                    new PerChannelTrcTransform([profile.GrayTrc]),
                    new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1)));
            }

            hasTrcModel = true;
        }
        else if (profile.Header.ColorSpace == IccColorSpace.Lab)
        {
            _modelTransform = IccTransforms.LabD50ToXyzTransform;
        }
        else if (profile.RedMatrix.HasValue && profile.GreenMatrix.HasValue && profile.BlueMatrix.HasValue)
        {
            List<IColorTransform> matrixTransforms = [];

            if (profile.RedTrc != null && profile.GreenTrc != null && profile.BlueTrc != null)
            {
                PerChannelTrcTransform trcLuts = new([profile.RedTrc, profile.GreenTrc, profile.BlueTrc]);
                matrixTransforms.Add(trcLuts);
                hasTrcModel = true;
            }

            MatrixColorTransform matrixTransform = new([profile.RedMatrix.Value, profile.GreenMatrix.Value, profile.BlueMatrix.Value]);
            matrixTransforms.Add(matrixTransform);

            _modelTransform = new ChainedColorTransform(matrixTransforms.ToArray());
        }
        else
        {
            _modelTransform = new ChainedColorTransform();
            hasModel = false;
        }

        for (int intentIndex = 0; intentIndex < IntentCount; intentIndex++)
        {
            IccLutPipeline? intentLut = profile.GetA2BLut((IccRenderingIntent)intentIndex) ?? profile.A2BLut0;
            if (intentLut == null && !hasModel)
            {
                intentLut = profile.A2BLut1 ?? profile.A2BLut2;
            }

            _intentLuts[intentIndex] = intentLut;
        }

        bool hasLut = profile.A2BLut0 != null
            || profile.A2BLut1 != null
            || profile.A2BLut2 != null;

        if (hasLut || hasTrcModel)
        {
            BuildBlackPointCompensationTransforms(hasTrcModel);
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
        if (resolvedIntent < IccRenderingIntent.Perceptual || resolvedIntent > IccRenderingIntent.AbsoluteColorimetric)
        {
            resolvedIntent = IccRenderingIntent.Perceptual;
        }

        IColorTransform? blackPointCompensation = null;
        if (useBlackPointCompensation)
        {
            blackPointCompensation = _blackPointCompensationTransforms[(int)resolvedIntent];
        }

        return new ChainedColorTransform(
            GetDeviceToPcsTransform(resolvedIntent),
            GetPcsToXyzTransform(resolvedIntent),
            blackPointCompensation,
            IccTransforms.XyzD50ToSrgbTransform);
    }

    private IColorTransform GetDeviceToPcsTransform(IccRenderingIntent intent)
    {
        IccLutPipeline? intentLut = _intentLuts[(int)intent];
        if (intentLut != null)
        {
            return intentLut.Transform;
        }

        return _modelTransform;
    }

    private IColorTransform? GetPcsToXyzTransform(IccRenderingIntent intent)
    {
        if (_intentLuts[(int)intent] != null)
        {
            return _lutPcsToXyzTransform;
        }

        return _modelPcsToXyzTransform;
    }

    private void BuildBlackPointCompensationTransforms(bool hasTrcModel)
    {
        for (int intentIndex = 0; intentIndex < IntentCount; intentIndex++)
        {
            Vector4? blackPoint = GetBlackPoint((IccRenderingIntent)intentIndex, hasTrcModel);
            if (blackPoint != null)
            {
                _blackPointCompensationTransforms[intentIndex] = IccTransforms.BuildBlackPointCompensationTransform(blackPoint.Value);
            }
        }
    }

    private Vector4? GetBlackPoint(IccRenderingIntent intent, bool hasTrcModel)
    {
        IccDeviceClass deviceClass = _iccProfile.Header.DeviceClass;
        if (deviceClass == IccDeviceClass.DeviceLink
            || deviceClass == IccDeviceClass.Abstract
            || deviceClass == IccDeviceClass.NamedColor)
        {
            return null;
        }

        IccProfileHeader header = _iccProfile.Header;
        IColorTransform relativeDeviceToPcs = GetDeviceToPcsTransform(IccRenderingIntent.RelativeColorimetric);
        IColorTransform? relativePcsToXyz = GetPcsToXyzTransform(IccRenderingIntent.RelativeColorimetric);

        Vector4? blackPoint = null;
        switch (intent)
        {
            case IccRenderingIntent.Perceptual:
            case IccRenderingIntent.Saturation:
                {
                    Version? version = header.Version;
                    if (version == null || version.Major < 4)
                    {
                        if (hasTrcModel || _iccProfile.HasA2BLut(intent))
                        {
                            blackPoint = IccBlackPoint.GetDarkestColorantBlackPoint(header, GetDeviceToPcsTransform(intent), GetPcsToXyzTransform(intent));
                        }
                    }
                    else if (hasTrcModel)
                    {
                        blackPoint = IccBlackPoint.GetDarkestColorantBlackPoint(header, relativeDeviceToPcs, relativePcsToXyz);
                    }
                    else
                    {
                        blackPoint = IccBlackPoint.PerceptualBlack;
                    }

                    break;
                }
            case IccRenderingIntent.RelativeColorimetric:
                {
                    IccLutPipeline? perceptualOutput = _iccProfile.B2ALut0;
                    if (deviceClass == IccDeviceClass.Output && IccBlackPoint.IsInkColorSpace(header))
                    {
                        if (perceptualOutput != null && (hasTrcModel || _iccProfile.HasA2BLut(IccRenderingIntent.Perceptual)))
                        {
                            blackPoint = IccBlackPoint.GetPerceptualBlackPoint(header, perceptualOutput, relativeDeviceToPcs, relativePcsToXyz);
                        }
                    }
                    else if (hasTrcModel || _iccProfile.HasA2BLut(intent))
                    {
                        blackPoint = IccBlackPoint.GetDarkestColorantBlackPoint(header, relativeDeviceToPcs, relativePcsToXyz);
                    }

                    break;
                }
        }

        return blackPoint;
    }
}
