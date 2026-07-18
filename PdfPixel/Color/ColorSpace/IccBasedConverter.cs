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
    private readonly bool _isStandardRgbOrGray;

    public IccBasedConverter(int n, PdfColorSpaceConverter? alternate, IccProfile? profile)
    {
        Profile = profile;
        N = n;

        _isStandardRgbOrGray = profile != null && (IccProfileAnalyzer.IsStandardSrgb(profile) || IccProfileAnalyzer.IsStandardGray(profile));
        if (profile == null || profile.ChannelsCount != n || _isStandardRgbOrGray)
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

    public override bool IsDevice => _isStandardRgbOrGray;

    public int N { get; }

    public override IColorTransform? NormalizeTransform => _default.NormalizeTransform;

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

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform? postTransform, bool normalize)
    {
        if (_useDefault || _iccTransform == null)
        {
            return _default.GetRgbaSampler(intent, postTransform, normalize);
        }

        IColorTransform iccPipeline = _iccTransform.GetIntentTransform(intent.ToIccRenderingIntent());
        IColorTransform? normalizeTransform = normalize ? _default.NormalizeTransform : null;
        ChainedColorTransform chained = new(normalizeTransform, iccPipeline, postTransform);
        return new ColorTransformSampler(chained);
    }
}
