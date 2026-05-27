using PdfPixel.Color.Icc;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using System;

namespace PdfPixel.Color.ColorSpace;

internal sealed class IccBasedConverter : PdfColorSpaceConverter
{
    private readonly bool _useDefault;
    private readonly PdfColorSpaceConverter _default;
    private readonly IccProfileTransform? _iccTransform;

    public IccBasedConverter(int n, PdfColorSpaceConverter? alternate, IccProfile? profile)
    {
        Profile = profile;
        N = n;

        //note that if alternate is LAB we expect input in LAB coordinates, since LAB does not define any color correction,
        // we simply fallback to alternate here, as ICC requires 0 - 1 input
        if (alternate is LabColorSpaceConverter || profile == null || profile.ChannelsCount != n || IccProfileAnalyzer.IsStandardSrgb(profile) || IccProfileAnalyzer.IsStandardGray(profile))
        {
            _useDefault = true;
        }
        else
        {
            _iccTransform = new IccProfileTransform(profile);
        }

        _default = alternate
            ?? n switch
                {
                    1 => DeviceGrayConverter.Instance,
                    3 => DeviceRgbConverter.Instance,
                    4 => DeviceCmykConverter.Instance,
                    _ => DeviceRgbConverter.Instance
                };
    }

    public IccBasedConverter(int n, PdfColorSpaceConverter? alternate, byte[]? iccProfileBytes)
        : this(n, alternate, GetProfile(iccProfileBytes))
    {
    }

    public IccProfile? Profile { get; }

    public override int Components => _default.Components;

    public override bool IsDevice => false;

    public int N { get; }

    private static IccProfile? GetProfile(byte[]? bytes)
    {
        if (bytes == null)
        {
            return null;
        }

#pragma warning disable CA1031
        try
        {
            return IccProfile.Parse(bytes);
        }
        catch (Exception)
        {
#if DEBUG
            throw;
#else
            return null;
#endif
        }
#pragma warning restore RCS1075
    }

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform? postTransform)
    {
        if (_useDefault || _iccTransform == null)
        {
            return _default.GetRgbaSampler(intent, postTransform);
        }

        return new ColorTransformSampler(new ChainedColorTransform(_iccTransform.GetIntentTransform(intent.ToIccRenderingIntent()), postTransform));
    }
}
