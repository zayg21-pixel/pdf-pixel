using PdfReader.Models;
using System;
using System.Numerics;

namespace PdfReader.Icc
{
    /// <summary>
    /// Converter for ICC Gray (single-channel) profiles to sRGB (0..1 float components).
    /// Handles:
    /// - Optional gray TRC (gamma / sampled / parametric) expansion via LUT.
    /// - White point handling (uses profile wtpt tag or falls back to header illuminant, else D50).
    /// - Black Point Compensation (relative colorimetric & others when requested by caller intent).
    /// This class purposefully ignores the profile header rendering intent; an explicit intent must
    /// be supplied per conversion call (PDF graphics state > RI entry or caller choice).
    /// </summary>
    internal sealed class IccGrayColorConverter
    {
        private readonly IccProfile _iccProfile;

        // Source white point (wtpt or header illuminant or D50) in XYZ D50-relative coordinates.
        private readonly Vector3 _whitePoint;
        private readonly float _inverseWhitePointY;
        private readonly Vector3 _whitePointYNormalized; // White scaled so Y = 1 (for absolute style intents)

        private readonly IccTrc _grayTrc;
        private readonly bool _hasGrayTrc;
        private readonly float[] _grayTrcLut;

        private readonly float _sourceBlackLstar;
        private readonly float _blackPointCompensationScale;

        /// <summary>
        /// Create a gray ICC profile converter.
        /// </summary>
        /// <param name="profile">Parsed ICC profile (must be gray space).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="profile"/> is null.</exception>
        public IccGrayColorConverter(IccProfile profile)
        {
            _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));

            Vector3? whitePointCandidate = GetWhitePointXyzVector(_iccProfile);
            _whitePoint = whitePointCandidate ?? IccProfileHelpers.D50WhitePoint;

            float whiteY = _whitePoint.Y;
            if (whiteY <= 0f)
            {
                whiteY = 1f; // Guard against invalid / zero Y
            }
            _inverseWhitePointY = 1f / whiteY;
            _whitePointYNormalized = new Vector3(
                _whitePoint.X * _inverseWhitePointY,
                1f,
                _whitePoint.Z * _inverseWhitePointY);

            if (_iccProfile.GrayTrc != null)
            {
                _grayTrc = _iccProfile.GrayTrc;
                _hasGrayTrc = true;
                _grayTrcLut = IccProfileHelpers.IccTrcToLut(_grayTrc, IccProfileHelpers.TrcLutSize);
            }

            _sourceBlackLstar = IccProfileHelpers.GetSourceBlackLstar(_iccProfile);
            _blackPointCompensationScale = IccProfileHelpers.GetBlackLstarScale(_sourceBlackLstar);
        }

        /// <summary>
        /// Convert a single normalized gray component (0..1) to sRGB (0..1) applying the specified rendering intent.
        /// </summary>
        /// <param name="gray01">Input gray component (0..1 range expected).</param>
        /// <param name="intent">Rendering intent (explicit; header intent is ignored).</param>
        /// <param name="rgb01">Resulting sRGB triplet (0..1 floats).</param>
        /// <returns>True on success (always true for valid input).</returns>
        public bool TryToSrgb01(float gray01, PdfRenderingIntent intent, out Vector3 rgb01)
        {
            rgb01 = default;

            float grayLinear;
            if (_hasGrayTrc)
            {
                // Map through pre-expanded TRC LUT to linearized luminance fraction.
                grayLinear = ColorMath.LookupLinear(_grayTrcLut, gray01);
            }
            else
            {
                grayLinear = gray01;
            }

            // Build source XYZ.
            // Relative colorimetric: scale D50 reference white by linear luminance (maps neutral axis using D50 reference).
            // Other intents: scale profile white (normalized to Y=1) by luminance (preserves original white chromaticity before PCS adaptation).
            Vector3 xyz;
            if (intent == PdfRenderingIntent.RelativeColorimetric)
            {
                float scale = grayLinear * _inverseWhitePointY;
                xyz = IccProfileHelpers.D50WhitePoint * scale;
            }
            else
            {
                xyz = _whitePointYNormalized * grayLinear;
            }

            // Apply Black Point Compensation (method encapsulates conditional use by intent).
            xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _sourceBlackLstar, _blackPointCompensationScale, intent);
            rgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
            return true;
        }

        /// <summary>
        /// Resolve white point XYZ (wtpt tag preferred, then header illuminant) as a vector.
        /// </summary>
        private static Vector3? GetWhitePointXyzVector(IccProfile profile)
        {
            if (profile.WhitePoint != null)
            {
                return new Vector3(profile.WhitePoint.Value.X, profile.WhitePoint.Value.Y, profile.WhitePoint.Value.Z);
            }

            if (profile.Header.Illuminant != null)
            {
                return new Vector3(profile.Header.Illuminant.Value.X, profile.Header.Illuminant.Value.Y, profile.Header.Illuminant.Value.Z);
            }

            return null;
        }
    }
}
