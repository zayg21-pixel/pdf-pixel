using PdfPixel.Color.Icc;
using PdfPixel.Color.Profiles;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

internal sealed class DeviceCmykConverter : PdfColorSpaceConverter
{
    public static readonly DeviceCmykConverter Instance = new();
    private static readonly IccProfileTransform _iccTransform;

    static DeviceCmykConverter()
    {
        Icc.Model.IccProfile cmykProfile = ProfileRespources.GetCmykProfile();
        _iccTransform = new IccProfileTransform(cmykProfile);
    }

    public override int Components => 4;

    public override bool IsDevice => true;

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
        => new(new ChainedColorTransform(_iccTransform.GetIntentTransform(intent.ToIccRenderingIntent()), postTransform));
}
