using PdfReader.Color.Icc.Converters;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Lut;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

internal sealed class IccBasedConverter : PdfColorSpaceConverter
{
    private readonly int _n;
    private readonly bool _useDefault;
    private readonly PdfColorSpaceConverter _default;
    private static readonly Vector3 _vectorToByte3 = new Vector3(255f);
    private static readonly Vector3 _vectorRounding3 = new Vector3(0.5f);
    private readonly IccGrayColorConverter _grayConverter;
    private readonly IccRgbColorConverter _rgbConverter;
    private readonly IccCmykColorConverter _cmykConverter;

    private IccBasedConverter(int n, PdfColorSpaceConverter alternate)
    {
        _n = n;

        if (alternate == null)
        {
            _default = n switch
            {
                1 => DeviceGrayConverter.Instance,
                3 => DeviceRgbConverter.Instance,
                4 => DeviceCmykConverter.Instance,
                _ => DeviceRgbConverter.Instance,
            };
        }
        else
        {
            _default = alternate;
        }
    }

    public IccBasedConverter(int n, PdfColorSpaceConverter alternate, IccProfile profile)
        : this(n, alternate)
    {
        Profile = profile;

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
                    _useDefault = true;
                    break;
            }
        }
        else
        {
            _useDefault = true;
        }
    }

    public IccBasedConverter(int n, PdfColorSpaceConverter alternate, byte[] iccProfileBytes)
        : this(n, alternate, IccProfile.Parse(iccProfileBytes))
    {
    }

    public IccProfile Profile { get; }

    public override int Components => _default.Components;

    public override bool IsDevice => false;

    public int N => _n;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
    {
        if (_useDefault || comps01.Length != N)
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
                converterUsed = _rgbConverter.TryToSrgb01(comps01, intent, out result);
                break;
            case 4:
                converterUsed = _cmykConverter.TryToSrgb01(comps01, intent, out result);
                break;
        }

        if (converterUsed)
        {
            Vector3 converted = ConvertTyByte(result);
            return new SKColor((byte)converted.X, (byte)converted.Y, (byte)converted.Z);
        }

        return _default.ToSrgb(comps01, intent);
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        if (_useDefault)
        {
            return new DefaultSampler(intent, ToSrgbCore);
        }

        switch (N)
        {
            case 1:
                return OneDLutGray.Build(intent, ToSrgbCore);
            case 3:
            {
                return TreeDLut.Build(intent, ToSrgbCore);
            }
            case 4:
            {
                return LayeredThreeDLut.Build(intent, ToSrgbCore);
            }
            default:
                return new DefaultSampler(intent, ToSrgbCore);
        }
    }

    private static Vector3 ConvertTyByte(Vector3 rgb01)
    {
        Vector3 converted = rgb01 * _vectorToByte3 + _vectorRounding3;
        return Vector3.Clamp(converted, Vector3.Zero, _vectorToByte3);
    }
}
