using PdfReader.Models;
using System;
using System.Numerics;

namespace PdfReader.Icc
{
    /// <summary>
    /// Optimized converter for ICC Gray profiles to sRGB.
    /// Encapsulates gray-only logic extracted from IccColorConverter without CMYK/RGB paths.
    /// </summary>
    internal sealed class IccGrayColorConverter
    {
        private readonly IccProfile _iccProfile;
        private readonly Vector3 _wpD50;
        private readonly float _invWpY;
        private readonly Vector3 _wpYNormalized;
        private readonly IccTrc _grayTrc;
        private readonly bool _hasGrayTrc;
        private readonly float _srcBlackL;
        private readonly float _bpcScale;
        private readonly float[] _grayTrcLut;

        public IccGrayColorConverter(IccProfile profile)
        {
            _iccProfile = profile ?? throw new ArgumentNullException(nameof(profile));

            var wpArr = IccProfileParser.GetWhitePointXYZ(_iccProfile);
            _wpD50 = (wpArr != null && wpArr.Length == 3)
                ? new Vector3(wpArr[0], wpArr[1], wpArr[2])
                : IccProfileHelpers.D50WhitePoint;

            float wpY = _wpD50.Y;
            if (wpY <= 0f)
            {
                wpY = 1f;
            }
            _invWpY = 1f / wpY;
            _wpYNormalized = new Vector3(_wpD50.X * _invWpY, 1f, _wpD50.Z * _invWpY);

            if (_iccProfile?.GrayTrc != null)
            {
                _grayTrc = _iccProfile.GrayTrc;
                _hasGrayTrc = true;
                _grayTrcLut = IccProfileHelpers.IccTrcToLut(_grayTrc, IccProfileHelpers.TrcLutSize);
            }

            _srcBlackL = IccProfileHelpers.GetSourceBlackLstar(_iccProfile);
            _bpcScale = IccProfileHelpers.GetBlackLstarScale(_srcBlackL);
        }

        public bool TryToSrgb01(float gray01, PdfRenderingIntent intent, out Vector3 rgb01)
        {
            rgb01 = default;

            float g01 = gray01;
            float Y;
            if (_hasGrayTrc)
            {
                Y = ColorMath.LookupLinear(_grayTrcLut, g01);
            }
            else
            {
                Y = g01;
            }

            Vector3 xyz;

            if (intent == PdfRenderingIntent.RelativeColorimetric)
            {
                float scale = Y * _invWpY;
                xyz = IccProfileHelpers.D50WhitePoint * scale;
            }
            else
            {
                xyz = _wpYNormalized * Y;
            }

            xyz = ColorMath.ApplyBlackPointCompensation(in xyz, _srcBlackL, _bpcScale,  intent);
            rgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
            return true;
        }
    }
}
