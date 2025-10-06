using System;
using System.Numerics;
using System.Collections.Concurrent;
using PdfReader.Models;

namespace PdfReader.Icc
{
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

        // Layered 3D LUT acceleration (slice-based CMY grids over selected K levels) cache per rendering intent.
        private readonly ConcurrentDictionary<PdfRenderingIntent, IccCmykLayered3dLut> _layeredLutCache = new ConcurrentDictionary<PdfRenderingIntent, IccCmykLayered3dLut>();

        // Delegate used for LUT slice population.
        private readonly CmykDeviceToSrgbCore _coreDelegate;

        public IccCmykColorConverter(IccProfile profile)
        {
            _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            _srcBlackL = IccProfileHelpers.GetSourceBlackLstar(_iccProfile);
            _bpcScale = IccProfileHelpers.GetBlackLstarScale(_srcBlackL);
            _coreDelegate = ConvertCore;
        }

        /// <summary>
        /// Convert a CMYK device color (0..1 components) to sRGB (0..1) given a rendering intent.
        /// Uses layered LUT acceleration when enabled and available; otherwise executes analytic pipeline.
        /// </summary>
        public bool TryToSrgb01(ReadOnlySpan<float> cmyk01, PdfRenderingIntent intent, out Vector3 srgb01)
        {
            srgb01 = default;

            if (cmyk01.Length < 4)
            {
                return false;
            }

            IccCmykLayered3dLut lut = _layeredLutCache.GetOrAdd(intent, i => IccCmykLayered3dLut.Build(i, _coreDelegate));
            if (lut != null && lut.HasSlices)
            {
                srgb01 = lut.Sample(cmyk01[0], cmyk01[1], cmyk01[2], cmyk01[3], SamlingInterpolation.SampleBilinear);
                return true;
            }

            return ConvertCore(cmyk01[0], cmyk01[1], cmyk01[2], cmyk01[3], intent, out srgb01);
        }

        /// <summary>
        /// Analytic CMYK -> sRGB conversion via ICC A2B pipeline (no layered LUT usage).
        /// </summary>
        private bool ConvertCore(float c, float m, float y, float k, PdfRenderingIntent intent, out Vector3 srgb01)
        {
            srgb01 = default;

            IccLutPipeline pipeline = IccProfileHelpers.GetA2BLutByIntent(_iccProfile, intent);
            if (pipeline == null || pipeline.InChannels < 4)
            {
                return false;
            }

            // Assemble CMYK sample array once per call (avoid new float[4] via stackalloc span)
            float[] input = new float[4] { c, m, y, k };
            float[] pcs = IccClutEvaluator.EvaluatePipelineToPcs(_iccProfile, pipeline, new ReadOnlySpan<float>(input));
            if (pcs == null || pcs.Length < 3)
            {
                return false;
            }

            if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeLab, StringComparison.Ordinal))
            {
                Vector3 xyz = ColorMath.LabD50ToXyz(pcs[0] * 100f, pcs[1] * 255f - 128f, pcs[2] * 255f - 128f);
                xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                return true;
            }

            if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeXYZ, StringComparison.Ordinal))
            {
                Vector3 xyz = new Vector3(pcs[0], pcs[1], pcs[2]);
                xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                return true;
            }

            return false;
        }
    }
}
