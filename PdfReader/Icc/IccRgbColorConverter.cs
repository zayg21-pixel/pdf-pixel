using System;
using System.Numerics;
using PdfReader.Models;

namespace PdfReader.Icc
{
    /// <summary>
    /// Converter for 3-component ICC-based (RGB or Lab) profiles to sRGB (0..1 float components).
    /// Supports:
    ///  - Matrix/TRC RGB profiles (fast path using profile rXYZ/gXYZ/bXYZ and rTRC/gTRC/bTRC)
    ///  - Lab device profiles (direct Lab -> XYZ -> sRGB)
    ///  - LUT-based A2B pipelines (mAB / lut8 / lut16) via <see cref="IccClutEvaluator"/>
    /// Black Point Compensation is applied based on supplied rendering intent (header intent is advisory only).
    /// </summary>
    internal sealed class IccRgbColorConverter
    {
        private readonly IccProfile _iccProfile;

        // Adapted profile matrix rows (device RGB -> PCS XYZ D50) when available.
        private readonly Vector3 _pcsRow0;
        private readonly Vector3 _pcsRow1;
        private readonly Vector3 _pcsRow2;

        private readonly bool _hasPerChannelTrc;            // All three TRCs present.
        private readonly bool _hasMatrixTrcProfile;         // Profile supplies matrix + (optionally) TRCs.

        // Expanded per-channel TRC lookup tables (linearized output 0..1) when TRCs present.
        private readonly float[] _trcLutR;
        private readonly float[] _trcLutG;
        private readonly float[] _trcLutB;

        private readonly float _sourceBlackLstar;
        private readonly float _blackPointCompensationScale;
        private readonly bool _isLabDevice; // Device space is Lab (ColorSpace == "Lab ")

        /// <summary>
        /// Create an RGB ICC converter instance for the specified profile.
        /// </summary>
        /// <param name="profile">Parsed ICC profile (RGB or Lab PCS/device).</param>
        /// <exception cref="ArgumentNullException">Thrown if profile is null.</exception>
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
                    _trcLutR = IccProfileHelpers.IccTrcToLut(profile.RedTrc, IccProfileHelpers.TrcLutSize);
                    _trcLutG = IccProfileHelpers.IccTrcToLut(profile.GreenTrc, IccProfileHelpers.TrcLutSize);
                    _trcLutB = IccProfileHelpers.IccTrcToLut(profile.BlueTrc, IccProfileHelpers.TrcLutSize);
                }
            }

            _sourceBlackLstar = IccProfileHelpers.GetSourceBlackLstar(_iccProfile);
            _blackPointCompensationScale = IccProfileHelpers.GetBlackLstarScale(_sourceBlackLstar);
        }

        /// <summary>
        /// Convert an RGB (or Lab device) triplet (0..1 normalized) to sRGB (0..1) according to the specified rendering intent.
        /// </summary>
        /// <param name="rgb01">Input components span (expects at least 3 values).</param>
        /// <param name="intent">Rendering intent controlling LUT selection / BPC usage.</param>
        /// <param name="srgb01">Output sRGB triplet (0..1 floats).</param>
        /// <returns>True on success; false if conversion not possible (e.g. insufficient channels or unsupported PCS).</returns>
        public bool TryToSrgb01(ReadOnlySpan<float> rgb01, PdfRenderingIntent intent, out Vector3 srgb01)
        {
            srgb01 = default;

            if (rgb01.Length < 3)
            {
                return false;
            }

            float c0 = rgb01[0];
            float c1 = rgb01[1];
            float c2 = rgb01[2];

            // Lab device path: profile device color space is Lab; only scaling/offset needed to produce Lab* components.
            if (_isLabDevice)
            {
                float L = c0 * 100f;              // 0..100
                float a = c1 * 255f - 128f;       // approximate -128..127
                float b = c2 * 255f - 128f;       // approximate -128..127
                Vector3 xyzFromLab = ColorMath.LabD50ToXyz(L, a, b);
                xyzFromLab = ColorMath.ApplyBlackPointCompensation(in xyzFromLab, _sourceBlackLstar, _blackPointCompensationScale, intent);
                srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyzFromLab);
                return true;
            }

            // Matrix + TRC fast path.
            if (_hasMatrixTrcProfile)
            {
                Vector3 linear;
                if (_hasPerChannelTrc)
                {
                    float rLinear = ColorMath.LookupLinear(_trcLutR, c0);
                    float gLinear = ColorMath.LookupLinear(_trcLutG, c1);
                    float bLinear = ColorMath.LookupLinear(_trcLutB, c2);
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

            // LUT-based fallback (A2B pipeline selection per explicit intent).
            IccLutPipeline pipeline = IccProfileHelpers.GetA2BLutByIntent(_iccProfile, intent);
            if (pipeline == null)
            {
                return false;
            }

            float[] pcs = IccClutEvaluator.EvaluatePipelineToPcs(_iccProfile, pipeline, rgb01);
            if (pcs == null || pcs.Length < 3)
            {
                return false;
            }

            // Interpret PCS.
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

        /// <summary>
        /// Build a 3x3 device RGB -> PCS matrix if profile includes rXYZ/gXYZ/bXYZ tags.
        /// </summary>
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
