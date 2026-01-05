using PdfReader.Color.Icc;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Transform;
using PdfReader.Color.Sampling;
using PdfReader.Color.Transform;

namespace PdfReader.Color.ColorSpace;

internal sealed class IccBasedConverter : PdfColorSpaceConverter
{
    private readonly bool _useDefault;
    private readonly PdfColorSpaceConverter _default;
    private readonly IccProfileTransform _iccTransform;

    public IccBasedConverter(int n, PdfColorSpaceConverter alternate, IccProfile profile)
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

    public IccBasedConverter(int n, PdfColorSpaceConverter alternate, byte[] iccProfileBytes)
        : this(n, alternate, IccProfile.Parse(iccProfileBytes))
    {
    }

    public IccProfile Profile { get; }

    public override int Components => _default.Components;

    public override bool IsDevice => false;

    public int N { get; }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        if (_useDefault)
        {
            return _default.GetRgbaSampler(intent, postTransform);
        }

        return new ColorTransformSampler(new ChainedColorTransform(_iccTransform.GetIntentTransform(intent), postTransform));
    }
}
