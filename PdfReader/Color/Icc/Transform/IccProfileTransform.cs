using PdfReader.Color.ColorSpace;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Utilities;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Icc.Transform;

internal sealed class IccProfileTransform
{
    private readonly bool _hasLut;
    private readonly IIccTransform _transform;
    private readonly IIccTransform _postTransform;
    private readonly IccProfile _iccProfile;

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
            _transform = new IccChainedTransform(
                new IccPerChannelLutTransform([profile.GrayTrc]),
                new IccFunctionTransform(x => x * 0.5f),
                new IccFunctionTransform(x => new Vector4(x.X, x.X, x.X, 1)));
        }
        else if (profile.Header.ColorSpace == IccConstants.SpaceLab)
        {
            _transform = IccTransforms.LabD50ToXyzTransform;
        }
        else if (profile.RedMatrix.HasValue && profile.GreenMatrix.HasValue && profile.BlueMatrix.HasValue)
        {
            List<IIccTransform> matrixTransforms = new List<IIccTransform>();

            if (profile.RedTrc != null && profile.GreenTrc != null && profile.BlueTrc != null)
            {
                var trcLuts = new IccPerChannelLutTransform([profile.RedTrc, profile.GreenTrc, profile.BlueTrc]);
                matrixTransforms.Add(trcLuts);
            }

            var matrixTransform = new IccMatrixTransform([profile.RedMatrix.Value, profile.GreenMatrix.Value, profile.BlueMatrix.Value]);
            matrixTransforms.Add(matrixTransform);

            matrixTransforms.Add(new IccFunctionTransform(x => x * 0.5f)); // by specification, matrix transform is 2.0 scale in comparison with LUT/mAB

            _transform = new IccChainedTransform(matrixTransforms.ToArray());
        }

        if (profile.Header.Pcs == IccConstants.TypeLab)
        {
            _postTransform = new IccChainedTransform(IccTransforms.LabD50ToXyzTransform, IccTransforms.XyzD50ToSrgbTransform);
        }
        else
        {
            _postTransform = new IccChainedTransform(new IccFunctionTransform(x => x * 2.0f), IccTransforms.XyzD50ToSrgbTransform);
        }
    }


    public int NChannels => _iccProfile.ChannelsCount;

    public bool IsValid => NChannels > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(ReadOnlySpan<float> values, PdfRenderingIntent intent)
    {
        var result = IccVectorUtilities.ToVector4WithOnePadding(values);

        if (!IsValid)
        {
            return result;
        }

        if (_hasLut)
        {
            IccLutPipeline pipeline = GetA2BLutByIntent(_iccProfile, intent);
            pipeline.Transform.Transform(ref result);
        }
        else
        {
            _transform.Transform(ref result);
        }

        _postTransform.Transform(ref result);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IccChainedTransform GetIntentTransform(PdfRenderingIntent intent)
    {
        List<IIccTransform> transforms =
        [
            _hasLut ? GetA2BLutByIntent(_iccProfile, intent).Transform : _transform,
            _postTransform,
        ];

        return new IccChainedTransform(transforms.ToArray());
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
