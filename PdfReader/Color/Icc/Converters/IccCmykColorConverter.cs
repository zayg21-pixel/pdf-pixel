using System;
using System.Numerics;
using PdfReader.Color.ColorSpace;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Utilities;

namespace PdfReader.Color.Icc.Converters;

/// <summary>
/// Converter for ICC CMYK device color spaces to sRGB using A2B LUT pipelines.
/// Provides optional layered 3D LUT acceleration (multiple CMY 3D slices at sampled K levels with linear K interpolation).
/// Falls back to analytic pipeline evaluation when LUT is unavailable or disabled.
/// </summary>
internal sealed class IccCmykColorConverter
{
    private readonly IccProfile _iccProfile;
    private readonly float _srcBlackL;
    private readonly float _bpcScale;

    public IccCmykColorConverter(IccProfile profile)
    {
        _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        _srcBlackL = IccProfileHelpers.GetSourceBlackLstar(_iccProfile);
        _bpcScale = IccProfileHelpers.GetBlackLstarScale(_srcBlackL);
    }

    public IccProfile IccProfile => _iccProfile;

    /// <summary>
    /// Convert a CMYK device color (0..1 components) to sRGB (0..1) given a rendering intent.
    /// Uses layered LUT acceleration when enabled and available; otherwise executes analytic pipeline.
    /// </summary>
    public bool TryToSrgb01(ReadOnlySpan<float> cmyk01, PdfRenderingIntent intent, out Vector3 srgb01)
    {
        srgb01 = default;

        IccLutPipeline pipeline = IccProfileHelpers.GetA2BLutByIntent(_iccProfile, intent);
        if (pipeline == null || pipeline.InChannels < 4)
        {
            return false;
        }

        float[] pcs = IccClutEvaluator.EvaluatePipelineToPcs(_iccProfile, pipeline, cmyk01);

        var xyzFromPcs = ColorMath.FromPcsToXyz(_iccProfile, pcs);

        if (xyzFromPcs == null)
        {
            return false;
        }

        Vector3 xyz = xyzFromPcs.Value;

        xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
        srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);

        return true;
    }
}
