using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Transform;
using PdfReader.Color.Lut;
using PdfReader.Color.Structures;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

internal sealed class IccBasedConverter : PdfColorSpaceConverter
{
    private readonly bool _useDefault;
    private readonly PdfColorSpaceConverter _default;
    private static readonly Vector4 _vectorToByte = new Vector4(255f);
    private static readonly Vector4 _vectorRounding = new Vector4(0.5f);
    private readonly IccProfileTransform _iccTransform;

    public IccBasedConverter(int n, PdfColorSpaceConverter alternate, IccProfile profile)
    {
        Profile = profile;
        N = n;

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

            // when alternate is Lab, we cannot use ICC profile either
            // this is violation from specs, but there's ambiguity in how to handle Lab coordinates
            // as LAB color space coordinates are in different ranges than 0..1
            _useDefault = alternate is LabColorSpaceConverter;
        }
    }

    public IccBasedConverter(int n, PdfColorSpaceConverter alternate, byte[] iccProfileBytes)
        : this(n, alternate, IccProfile.Parse(iccProfileBytes))
    {
    }

    public IccProfile Profile { get; }

    public override int Components => _default.Components;

    public override bool IsDevice => false;

    public int N { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
    {
        if (_useDefault)
        {
            return _default.GetRgbaSampler(intent).SampleColor(comps01);
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

        return new IccSampler(intent, _iccTransform);
    }

    //protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    //{
    //    if (_useDefault)
    //    {
    //        return _default.GetRgbaSampler(intent);
    //    }

    //    switch (N)
    //    {
    //        case 1:
    //            return IccClutTransform.Build(intent, ToSrgbCore, 3, 1, 256);
    //        case 3:
    //            return IccClutTransform.Build(intent, ToSrgbCore, 3, 3, 16);
    //        case 4:
    //            return IccClutTransform.Build(intent, ToSrgbCore, 3, 4, 16);
    //        default:
    //            return new DefaultSampler(intent, ToSrgbCore);
    //    }
    //}

    private sealed class IccSampler : IRgbaSampler
    {
        public bool IsDefault => false;
        private readonly IccChainedTransform _transforms;

        public IccSampler(PdfRenderingIntent intent, IccProfileTransform iccProfileTransform)
        {
            _transforms = iccProfileTransform.GetIntentTransform(intent);
        }

        public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
        {
            var value = IccVectorUtilities.ToVector4WithOnePadding(source);
            _transforms.Transform(ref value);
            value *= 255;
            destination = new RgbaPacked((byte)value.X, (byte)value.Y, (byte)value.Z, 255);
        }

        public SKColor SampleColor(ReadOnlySpan<float> source)
        {
            var value = IccVectorUtilities.ToVector4WithOnePadding(source);
            _transforms.Transform(ref value);
            value *= 255;
            return new SKColor((byte)value.X, (byte)value.Y, (byte)value.Z);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 ConvertTyByte(Vector4 rgb01)
    {
        Vector4 converted = rgb01 * _vectorToByte + _vectorRounding;
        return Vector4.Clamp(converted, Vector4.Zero, _vectorToByte);
    }
}
