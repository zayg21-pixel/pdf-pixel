using PdfPixel.Color.ColorSpace;
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
internal sealed class IccProfileTransform
{
    private readonly bool _hasLut;
    private readonly IColorTransform _transform;
    private readonly IColorTransform _postTransform;
    private readonly IccProfile _iccProfile;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfileTransform"/> class using the specified ICC profile.
    /// </summary>
    /// <param name="profile">The ICC profile to use for color transformation.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="profile"/> is null.</exception>
    public IccProfileTransform(IccProfile profile)
    {
        _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));

        var lut = GetA2BLutByIntent(profile, PdfRenderingIntent.RelativeColorimetric);

        if (lut != null)
        {
            _hasLut = true;
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
            List<IColorTransform> matrixTransforms = new List<IColorTransform>();

            if (profile.RedTrc != null && profile.GreenTrc != null && profile.BlueTrc != null)
            {
                var trcLuts = new PerChannelTrcTransform([profile.RedTrc, profile.GreenTrc, profile.BlueTrc]);
                matrixTransforms.Add(trcLuts);
            }

            var matrixTransform = new MatrixColorTransform([profile.RedMatrix.Value, profile.GreenMatrix.Value, profile.BlueMatrix.Value]);
            matrixTransforms.Add(matrixTransform);

            _transform = new ChainedColorTransform(matrixTransforms.ToArray());
        }

        if (profile.Header.Pcs == IccColorSpace.Lab)
        {
            _postTransform = new ChainedColorTransform(IccTransforms.LabD50ToXyzTransform, IccTransforms.XyzD50ToSrgbTransform);
        }
        else
        {
            if (_hasLut)
            {
                _postTransform = new ChainedColorTransform(new FunctionColorTransform(x => x * 2.0f), IccTransforms.XyzD50ToSrgbTransform);
            }
            else
            {
                _postTransform = IccTransforms.XyzD50ToSrgbTransform;
            }
        }
    }

    /// <summary>
    /// Gets a chained color transform for the specified PDF rendering intent.
    /// </summary>
    /// <param name="intent">The PDF rendering intent to use for the transformation.</param>
    /// <returns>A <see cref="ChainedColorTransform"/> representing the color transformation pipeline.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChainedColorTransform GetIntentTransform(PdfRenderingIntent intent)
    {
        List<IColorTransform> transforms =
        [
            _hasLut ? GetA2BLutByIntent(_iccProfile, intent).Transform : _transform,
            _postTransform,
        ];

        return new ChainedColorTransform(transforms.ToArray());
    }

    /// <summary>
    /// Select appropriate parsed A2B LUT pipeline by explicit PDF rendering intent with ordered fallback.
    /// Header rendering intent is advisory and ignored here.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IccLutPipeline GetA2BLutByIntent(IccProfile profile, PdfRenderingIntent intent)
    {
        if (profile == null)
        {
            return null;
        }

        switch (intent)
        {
            case PdfRenderingIntent.Perceptual:
                return profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;
            case PdfRenderingIntent.RelativeColorimetric:
                return profile.A2BLut1 ?? profile.A2BLut0 ?? profile.A2BLut2;
            case PdfRenderingIntent.Saturation:
                return profile.A2BLut2 ?? profile.A2BLut0 ?? profile.A2BLut1;
            case PdfRenderingIntent.AbsoluteColorimetric:
                return profile.A2BLut1 ?? profile.A2BLut0 ?? profile.A2BLut2;
            default:
                return profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;
        }
    }
}
