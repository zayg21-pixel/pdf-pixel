using PdfReader.Icc;
using PdfReader.Models;
using SkiaSharp;
using System;
using System.Numerics;

namespace PdfReader.Rendering.Color
{
    internal sealed class IccBasedConverter : PdfColorSpaceConverter
    {
        private readonly int _n;
        private readonly PdfColorSpaceConverter _alternate;
        private readonly bool _useAlternate;
        private readonly PdfColorSpaceConverter _default;
        private static readonly Vector3 _vectorToByte3 = new Vector3(255f);
        private static readonly Vector3 _vectorRounding3 = new Vector3(0.5f);
        private readonly IccGrayColorConverter _grayConverter;
        private readonly IccRgbColorConverter _rgbConverter;
        private readonly IccCmykColorConverter _cmykConverter;

        public IccBasedConverter(int n, PdfColorSpaceConverter alternate)
        {
            _n = n;
            _alternate = alternate;
            _default = _alternate ?? (_n == 1 ? DeviceGrayConverter.Instance
                                              : _n == 4 ? DeviceCmykConverter.Instance
                                                         : DeviceRgbConverter.Instance);
        }

        public IccBasedConverter(int n, PdfColorSpaceConverter alternate, byte[] iccProfileBytes)
            : this(n, alternate)
        {
            var profile = IccProfileParser.TryParse(iccProfileBytes);

            if (profile != null)
            {
                switch (n)
                {
                    case 1:
                        _grayConverter = new IccGrayColorConverter(profile);
                        break;
                    case 3:
                        _rgbConverter = new IccRgbColorConverter(profile);
                        break;
                    case 4:
                        _cmykConverter = new IccCmykColorConverter(profile);
                        break;
                    default:
                        _useAlternate = true;
                        break;
                }
            }
            else
            {
                _useAlternate = true;
            }
        }

        public override int Components => _default.Components;

        public override bool IsDevice => false;

        public int N => _n;

        public override SKColor ToSrgb(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
        {
            if (_useAlternate || comps01.Length != N)
            {
                return _default.ToSrgb(comps01, intent);
            }

            Vector3 result = default;
            bool converterUsed = false;

            switch (comps01.Length)
            {
                case 1:
                    converterUsed = _grayConverter.TryToSrgb01(comps01[0], intent, out result);
                    break;
                case 3:
                    converterUsed = _rgbConverter.TryToSrgb01(new Vector3(comps01[0], comps01[1], comps01[2]), intent, out result);
                    break;
                case 4:
                    converterUsed = _cmykConverter.TryToSrgb01(new Vector4(comps01[0], comps01[1], comps01[2], comps01[3]), intent, out result);
                    break;
            }

            if (converterUsed)
            {
                Vector3 converted = ConvertTyByte(result);
                return new SKColor((byte)converted.X, (byte)converted.Y, (byte)converted.Z);
            }

            return _default.ToSrgb(comps01, intent);
        }

        private static Vector3 ConvertTyByte(Vector3 rgb01)
        {
            Vector3 converted = rgb01 * _vectorToByte3 + _vectorRounding3;
            return Vector3.Clamp(converted, Vector3.Zero, _vectorToByte3);
        }
    }
}
