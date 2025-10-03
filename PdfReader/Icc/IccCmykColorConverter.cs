using System;
using System.Numerics;
using PdfReader.Models;
using System.Collections.Generic;

namespace PdfReader.Icc
{
    /// <summary>
    /// Optimized converter for ICC CMYK (LUT-based) profiles to sRGB.
    /// Encapsulates the CMYK-only path extracted from IccColorConverter, using
    /// A2B LUT pipelines and Black Point Compensation.
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

        public bool TryToSrgb01(Vector4 cmyk01, PdfRenderingIntent intent, out Vector3 rgb01)
        {
            rgb01 = default;

            // Use ICC A2B LUT when available.
            var lut = GetA2BLutByIntent(intent);
            if (lut == null || lut.InChannels != 4)
            {
                return false;
            }

            float c = cmyk01.X;
            float m = cmyk01.Y;
            float y = cmyk01.Z;
            float k = cmyk01.W;

            float[] vin = new float[4];
            vin[0] = ApplyTable(lut.InputTables[0], c);
            vin[1] = ApplyTable(lut.InputTables[1], m);
            vin[2] = ApplyTable(lut.InputTables[2], y);
            vin[3] = ApplyTable(lut.InputTables[3], k);

            if (lut.IsMab)
            {
                // mAB path: reuse pipeline evaluators
                float[] pcs = EvaluateMabToPcs(lut, vin);
                if (pcs == null || pcs.Length < 3)
                {
                    return false;
                }

                bool applyBpc = intent == PdfRenderingIntent.RelativeColorimetric;
                if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeLab, StringComparison.Ordinal))
                {
                    var xyz = ColorMath.LabD50ToXyz(pcs[0] * 100f, pcs[1] * 255f - 128f, pcs[2] * 255f - 128f);
                    var xyzBpc = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                    rgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzBpc);
                    return true;
                }

                if (!string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeXYZ, StringComparison.Ordinal))
                {
                    return false;
                }

                {
                    var xyz = new Vector3(pcs[0], pcs[1], pcs[2]);
                    var xyzBpc = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                    rgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzBpc);
                    return true;
                }
            }
            else
            {
                // lut8/16 path
                float[] vclut = IccClutEvaluator.EvaluateClutLinear(lut, vin);

                int outCh = lut.OutChannels;
                if (outCh < 3)
                {
                    return false;
                }

                float[] vout = new float[outCh];
                for (int cIdx = 0; cIdx < outCh; cIdx++)
                {
                    vout[cIdx] = ApplyTable(lut.OutputTables[cIdx], vclut[cIdx]);
                }

                bool applyBpc = intent == PdfRenderingIntent.RelativeColorimetric;
                if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeLab, StringComparison.Ordinal))
                {
                    var xyz = ColorMath.LabD50ToXyz(vout[0] * 100f, vout[1] * 255f - 128f, vout[2] * 255f - 128f);
                    var xyzBpc = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                    rgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzBpc);
                    return true;
                }

                if (!string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeXYZ, StringComparison.Ordinal))
                {
                    return false;
                }

                {
                    var xyz = new Vector3(vout[0], vout[1], vout[2]);
                    var xyzBpc = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                    rgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzBpc);
                    return true;
                }
            }
        }

        private IccLutPipeline GetA2BLutByIntent(PdfRenderingIntent intent)
        {
            if (_iccProfile == null)
            {
                return null;
            }

            switch (intent)
            {
                case PdfRenderingIntent.Perceptual:
                    return _iccProfile.A2BLut0 ?? _iccProfile.A2BLut1 ?? _iccProfile.A2BLut2;
                case PdfRenderingIntent.RelativeColorimetric:
                    return _iccProfile.A2BLut1 ?? _iccProfile.A2BLut0 ?? _iccProfile.A2BLut2;
                case PdfRenderingIntent.Saturation:
                    return _iccProfile.A2BLut2 ?? _iccmykLutFallback();
                case PdfRenderingIntent.AbsoluteColorimetric:
                    return _iccProfile.A2BLut1 ?? _iccProfile.A2BLut0 ?? _iccProfile.A2BLut2;
                default:
                    return _iccProfile.A2BLut0 ?? _iccProfile.A2BLut1 ?? _iccProfile.A2BLut2;
            }

            IccLutPipeline _iccmykLutFallback() => _iccProfile.A2BLut0 ?? _iccProfile.A2BLut1 ?? _iccProfile.A2BLut2;
        }

        private static float[] EvaluateMabToPcs(IccLutPipeline p, IReadOnlyList<float> comps01)
        {
            int n = Math.Min(4, p.InChannels);
            var aOut = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = i < comps01.Count ? Clamp01(comps01[i]) : 0f;
                aOut[i] = IccTrcEvaluator.ApplyTrc(p.CurvesA, i, v);
            }

            float[] mOut = aOut;
            if (p.Matrix3x3 != null && p.MatrixOffset != null && p.Matrix3x3.Length == 9 && p.MatrixOffset.Length == 3)
            {
                mOut = new float[3];
                float x = p.Matrix3x3[0, 0] * aOut[0] + p.Matrix3x3[0, 1] * aOut[1] + p.Matrix3x3[0, 2] * aOut[2] + p.MatrixOffset[0];
                float y = p.Matrix3x3[1, 0] * aOut[0] + p.Matrix3x3[1, 1] * aOut[1] + p.Matrix3x3[1, 2] * aOut[2] + p.MatrixOffset[1];
                float z = p.Matrix3x3[2, 0] * aOut[0] + p.Matrix3x3[2, 1] * aOut[1] + p.Matrix3x3[2, 2] * aOut[2] + p.MatrixOffset[2];
                mOut[0] = x; mOut[1] = y; mOut[2] = z;
            }

            var clutOut = IccClutEvaluator.EvaluateClutLinearMab(p, mOut);

            for (int i = 0; i < clutOut.Length && i < (p.CurvesM?.Length ?? 0); i++)
            {
                clutOut[i] = IccTrcEvaluator.ApplyTrc(p.CurvesM, i, clutOut[i]);
            }

            var pcs = new float[p.OutChannels];
            for (int i = 0; i < pcs.Length; i++)
            {
                pcs[i] = IccTrcEvaluator.ApplyTrc(p.CurvesB, i, i < clutOut.Length ? clutOut[i] : 0f);
            }

            return pcs;
        }

        private static float ApplyTable(float[] table, float x)
        {
            if (table == null || table.Length == 0)
            {
                return x;
            }

            int n = table.Length;
            float pos = x * (n - 1);
            if (pos <= 0f)
            {
                return table[0];
            }

            if (pos >= n - 1)
            {
                return table[n - 1];
            }

            int i = (int)pos;
            float f = pos - i;
            float y0 = table[i];
            float y1 = table[i + 1];
            return y0 + (y1 - y0) * f;
        }

        private static float Clamp01(float v)
        {
            if (v <= 0f)
            {
                return 0f;
            }

            if (v >= 1f)
            {
                return 1f;
            }

            return v;
        }
    }
}
