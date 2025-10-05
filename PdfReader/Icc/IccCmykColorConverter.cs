using System;
using System.Numerics;
using PdfReader.Models;

namespace PdfReader.Icc
{
    /// <summary>
    /// Converter for ICC CMYK device color spaces to sRGB using A2B LUT pipelines.
    /// Reuses shared pipeline evaluation logic from <see cref="IccClutEvaluator"/>.
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

        public bool TryToSrgb01(ReadOnlySpan<float> cmyk01, PdfRenderingIntent intent, out Vector3 srgb01)
        {
            srgb01 = default;

            if (cmyk01.Length < 4)
            {
                return false;
            }

            var pipeline = IccProfileHelpers.GetA2BLutByIntent(_iccProfile, intent);
            if (pipeline == null || pipeline.InChannels < 4)
            {
                return false;
            }

            float[] pcs = IccClutEvaluator.EvaluatePipelineToPcs(_iccProfile, pipeline, cmyk01);
            if (pcs == null || pcs.Length < 3)
            {
                return false;
            }

            if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeLab, StringComparison.Ordinal))
            {
                var xyz = ColorMath.LabD50ToXyz(pcs[0] * 100f, pcs[1] * 255f - 128f, pcs[2] * 255f - 128f);
                xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                return true;
            }

            if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeXYZ, StringComparison.Ordinal))
            {
                var xyz = new Vector3(pcs[0], pcs[1], pcs[2]);
                xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                return true;
            }

            return false;
        }
    }
}
