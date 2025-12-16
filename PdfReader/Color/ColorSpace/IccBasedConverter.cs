using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Transform;
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
    private static readonly Vector4 _vectorToByte = new Vector4(255f);
    private static readonly Vector4 _vectorRounding = new Vector4(0.5f);
    private readonly IccProfileTransform _iccTransform;

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
            _iccTransform = new IccProfileTransform(profile);

            if (!_iccTransform.IsValid || _iccTransform.NChannels != n)
            {
                _useDefault = true;
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
        if (_useDefault)
        {
            return _default.ToSrgb(comps01, intent);
        }

        var result = _iccTransform.Transform(comps01, intent);

        var converted = ConvertTyByte(result);

        return new SKColor((byte)converted.X, (byte)converted.Y, (byte)converted.Z);
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        if (_useDefault)
        {
            return _default.GetRgbaSampler(intent);
        }

        switch (N)
        {
            case 1:
                return OneDLutGray.Build(intent, ToSrgbCore);
            case 3:
            {
                return ThreeDLut.Build(intent, ToSrgbCore);
            }
            case 4:
            {
                return LayeredThreeDLut.Build(intent, ToSrgbCore);
            }
            default:
                return new DefaultSampler(intent, ToSrgbCore);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 ConvertTyByte(Vector4 rgb01)
    {
        Vector4 converted = rgb01 * _vectorToByte + _vectorRounding;
        return Vector4.Clamp(converted, Vector4.Zero, _vectorToByte);
    }
}
