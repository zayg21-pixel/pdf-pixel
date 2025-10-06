using System;
using System.Numerics;
using System.Collections.Concurrent;
using PdfReader.Models;

namespace PdfReader.Icc
{
    internal sealed class IccRgbColorConverter
    {
        private readonly IccProfile _iccProfile;
        private readonly Vector3 _pcsRow0;
        private readonly Vector3 _pcsRow1;
        private readonly Vector3 _pcsRow2;
        private readonly bool _hasPerChannelTrc;
        private readonly bool _hasMatrixTrcProfile;
        private readonly float _sourceBlackLstar;
        private readonly float _blackPointCompensationScale;
        private readonly bool _isLabDevice;

        private readonly ConcurrentDictionary<PdfRenderingIntent, Vector3[]> _intentLutCache = new ConcurrentDictionary<PdfRenderingIntent, Vector3[]>();

        private readonly RgbDeviceToSrgbCore _coreDelegate; // Delegate used for LUT population.

        public IccRgbColorConverter(IccProfile profile)
        {
            _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));

            _isLabDevice = string.Equals(
                _iccProfile?.Header?.ColorSpace,
                IccConstants.SpaceLab,
                StringComparison.Ordinal);

            if (!_isLabDevice)
            {
                if (TryBuildDeviceToPcsMatrix(profile, out float[,] matrix3x3))
                {
                    _hasMatrixTrcProfile = true;
                    (_pcsRow0, _pcsRow1, _pcsRow2) = IccProfileHelpers.AdaptRgbMatrixToPcsRows(profile, matrix3x3);
                }

                if (profile.RedTrc != null && profile.GreenTrc != null && profile.BlueTrc != null)
                {
                    _hasPerChannelTrc = true;
                }
            }

            _sourceBlackLstar = IccProfileHelpers.GetSourceBlackLstar(_iccProfile);
            _blackPointCompensationScale = IccProfileHelpers.GetBlackLstarScale(_sourceBlackLstar);

            // Assign delegate to instance method.
            _coreDelegate = ConvertCore;
        }

        public bool TryToSrgb01(ReadOnlySpan<float> rgb01, PdfRenderingIntent intent, out Vector3 srgb01)
        {
            srgb01 = default;

            if (rgb01.Length < 3)
            {
                return false;
            }

            // Thread-safe LUT creation (null cached if build yields no successful samples).
            Vector3[] lut = _intentLutCache.GetOrAdd(intent, i => IccRgb3dLut.Build(i, _coreDelegate));
            if (lut != null && lut.Length > 0)
            {
                srgb01 = IccRgb3dLut.Sample(lut, rgb01[0], rgb01[1], rgb01[2], SamlingInterpolation.SampleBilinear);
                return true;
            }

            // Fallback analytic path (if LUT build produced only failures or is absent).
            return ConvertCore(rgb01[0], rgb01[1], rgb01[2], intent, out srgb01);
        }

        private bool ConvertCore(float c0, float c1, float c2, PdfRenderingIntent intent, out Vector3 srgb01)
        {
            srgb01 = default;

            if (_isLabDevice)
            {
                float L = c0 * 100f;
                float a = c1 * 255f - 128f;
                float b = c2 * 255f - 128f;
                Vector3 xyzFromLab = ColorMath.LabD50ToXyz(L, a, b);
                xyzFromLab = ColorMath.ApplyBlackPointCompensation(in xyzFromLab, _sourceBlackLstar, _blackPointCompensationScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzFromLab);
                return true;
            }

            if (_hasMatrixTrcProfile)
            {
                Vector3 linear;
                if (_hasPerChannelTrc)
                {
                    float rLinear = IccTrcEvaluator.EvaluateTrc(_iccProfile.RedTrc, c0);
                    float gLinear = IccTrcEvaluator.EvaluateTrc(_iccProfile.GreenTrc, c1);
                    float bLinear = IccTrcEvaluator.EvaluateTrc(_iccProfile.BlueTrc, c2);
                    linear = new Vector3(rLinear, gLinear, bLinear);
                }
                else
                {
                    linear = new Vector3(c0, c1, c2);
                }

                float X = Vector3.Dot(_pcsRow0, linear);
                float Y = Vector3.Dot(_pcsRow1, linear);
                float Z = Vector3.Dot(_pcsRow2, linear);
                Vector3 xyz = new Vector3(X, Y, Z);
                xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _sourceBlackLstar, _blackPointCompensationScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
                return true;
            }

            IccLutPipeline pipeline = IccProfileHelpers.GetA2BLutByIntent(_iccProfile, intent);
            if (pipeline == null)
            {
                return false;
            }

            float[] pcs = IccClutEvaluator.EvaluatePipelineToPcs(_iccProfile, pipeline, new ReadOnlySpan<float>(new[] { c0, c1, c2 }));
            if (pcs == null || pcs.Length < 3)
            {
                return false;
            }

            if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeLab, StringComparison.Ordinal))
            {
                Vector3 xyzConv = ColorMath.LabD50ToXyz(
                    pcs[0] * 100f,
                    pcs[1] * 255f - 128f,
                    pcs[2] * 255f - 128f);
                xyzConv = ColorMath.ApplyBlackPointCompensation(in xyzConv, _sourceBlackLstar, _blackPointCompensationScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzConv);
                return true;
            }

            if (string.Equals(_iccProfile?.Header?.Pcs, IccConstants.TypeXYZ, StringComparison.Ordinal))
            {
                Vector3 xyzConv = new Vector3(pcs[0], pcs[1], pcs[2]);
                xyzConv = ColorMath.ApplyBlackPointCompensation(in xyzConv, _sourceBlackLstar, _blackPointCompensationScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzConv);
                return true;
            }

            return false;
        }

        private static bool TryBuildDeviceToPcsMatrix(IccProfile profile, out float[,] matrix)
        {
            matrix = null;
            if (profile?.RedMatrix == null || profile.GreenMatrix == null || profile.BlueMatrix == null)
            {
                return false;
            }

            matrix = new float[3, 3];
            matrix[0, 0] = profile.RedMatrix.Value.X;
            matrix[0, 1] = profile.GreenMatrix.Value.X;
            matrix[0, 2] = profile.BlueMatrix.Value.X;
            matrix[1, 0] = profile.RedMatrix.Value.Y;
            matrix[1, 1] = profile.GreenMatrix.Value.Y;
            matrix[1, 2] = profile.BlueMatrix.Value.Y;
            matrix[2, 0] = profile.RedMatrix.Value.Z;
            matrix[2, 1] = profile.GreenMatrix.Value.Z;
            matrix[2, 2] = profile.BlueMatrix.Value.Z;
            return true;
        }
    }
}
