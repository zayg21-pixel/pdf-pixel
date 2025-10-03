using System;
using System.Numerics;
using PdfReader.Models;
using System.Collections.Generic;

namespace PdfReader.Icc
{
    /// <summary>
    /// Converter for 3-component ICC-based color spaces to sRGB.
    /// Supports:
    ///  - RGB matrix/TRC profiles (fast path)
    ///  - RGB LUT-based profiles via A2B pipelines
    ///  - Lab device profiles (ColorSpace == "Lab ") by direct Lab->XYZ->sRGB conversion
    /// </summary>
    internal sealed class IccRgbColorConverter
    {
        private readonly IccProfile _iccProfile;
        private readonly Vector3 _PscRow0;
        private readonly Vector3 _PcsRow1;
        private readonly Vector3 _PcsRow2;
        private readonly bool _hasTrc;

        private readonly float[] _rgbTrcLutsR; // per-channel TRC LUTs
        private readonly float[] _rgbTrcLutsG; // per-channel TRC LUTs
        private readonly float[] _rgbTrcLutsB; // per-channel TRC LUTs
        private readonly bool _hasRgbMatrixTrc;
        private readonly float _srcBlackL;
        private readonly float _bpcScale;
        private readonly bool _isLabDevice;

        public IccRgbColorConverter(IccProfile profile)
        {
            _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            _isLabDevice = string.Equals(_iccProfile?.Header?.ColorSpace, IccConstants.SpaceLab, StringComparison.Ordinal);

            if (!_isLabDevice)
            {
                var mt = IccProfileParser.GetRgbMatrixTrc(profile);
                if (mt.HasValue)
                {
                    _hasRgbMatrixTrc = true;
                    // Adapt matrix to PCS (D50) using CHAD if present, expose rows as Vector3
                    (_PscRow0, _PcsRow1, _PcsRow2) = IccProfileHelpers.AdaptRgbMatrixToPcsRows(profile, mt.Value.m);

                    // Prefer per-channel TRCs if present
                    if (profile.RedTrc != null && profile.GreenTrc != null && profile.BlueTrc != null)
                    {
                        _hasTrc = true;
                        _rgbTrcLutsR = IccProfileHelpers.IccTrcToLut(profile.RedTrc, IccProfileHelpers.TrcLutSize);
                        _rgbTrcLutsG = IccProfileHelpers.IccTrcToLut(profile.GreenTrc, IccProfileHelpers.TrcLutSize);
                        _rgbTrcLutsB = IccProfileHelpers.IccTrcToLut(profile.BlueTrc, IccProfileHelpers.TrcLutSize);
                    }
                }
            }

            _srcBlackL = IccProfileHelpers.GetSourceBlackLstar(_iccProfile);
            _bpcScale = IccProfileHelpers.GetBlackLstarScale(_srcBlackL);
        }

        public bool TryToSrgb01(Vector3 comps01, PdfRenderingIntent intent, out Vector3 srgb01)
        {
            srgb01 = default;
            
            // Lab device profile direct path
            if (_isLabDevice)
            {
                float L = comps01.X * 100f;        // 0..100
                float a = comps01.Y * 255f - 128f; // -128..127 approx
                float b = comps01.Z * 255f - 128f; // -128..127 approx
                var xyzLab = ColorMath.LabD50ToXyz(L, a, b);
                xyzLab = ColorMath.ApplyBlackPointCompensation(in xyzLab, _srcBlackL, _bpcScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzLab);
                return true;
            }

            // Matrix/TRC fast path for RGB
            if (_hasRgbMatrixTrc)
            {
                Vector3 lin;
                if (_hasTrc)
                {
                    float rl = ColorMath.LookupLinear(_rgbTrcLutsR, comps01.X);
                    float gr = ColorMath.LookupLinear(_rgbTrcLutsG, comps01.Y);
                    float gb = ColorMath.LookupLinear(_rgbTrcLutsB, comps01.Z);
                    lin = new Vector3(rl, gr, gb);
                }
                else
                {
                    lin = comps01;
                }

                float X = Vector3.Dot(_PscRow0, lin);
                float Y = Vector3.Dot(_PcsRow1, lin);
                float Z = Vector3.Dot(_PcsRow2, lin);
                var xyz = new Vector3(X, Y, Z);
                xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                return true;
            }

            // TODO: optimize later!!! (e.g. pooled arrays, vectorization, tetrahedral CLUT)

            // LUT-based fallback
            var lut = GetA2BLutByIntent(intent);
            if (lut == null || lut.InChannels < 3)
            {
                return false;
            }

            float[] vin = new float[3];
            vin[0] = ApplyTable(lut.InputTables[0], comps01.X);
            vin[1] = ApplyTable(lut.InputTables[1], comps01.Y);
            vin[2] = ApplyTable(lut.InputTables[2], comps01.Z);

            if (lut.IsMab)
            {
                float[] pcs = EvaluateMabToPcs(lut, vin);
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

                if (!string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeXYZ, StringComparison.Ordinal))
                {
                    return false;
                }

                {
                    var xyz = new Vector3(pcs[0], pcs[1], pcs[2]);
                    xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                    srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                    return true;
                }
            }
            else
            {
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

                if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeLab, StringComparison.Ordinal))
                {
                    var xyz = ColorMath.LabD50ToXyz(vout[0] * 100f, vout[1] * 255f - 128f, vout[2] * 255f - 128f);
                    xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                    srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                    return true;
                }

                if (!string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeXYZ, StringComparison.Ordinal))
                {
                    return false;
                }

                {
                    var xyz = new Vector3(vout[0], vout[1], vout[2]);
                    xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale, intent);
                    srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                    return true;
                }
            }

            return false;
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
            int n = Math.Min(3, p.InChannels);
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
                mOut[0] = x;
                mOut[1] = y;
                mOut[2] = z;
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
