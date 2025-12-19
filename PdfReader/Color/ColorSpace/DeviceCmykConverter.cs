using PdfReader.Color.Icc;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Sampling;
using PdfReader.Resources;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Provides a converter for the Device CMYK color space to sRGB.
/// </summary>
/// <remarks>This converter uses an ICC profile to accurately transform CMYK color values to sRGB. It is designed
/// to handle the Device CMYK color space, which is commonly used in printing.</remarks>
internal sealed class DeviceCmykConverter : PdfColorSpaceConverter
{
    public static readonly DeviceCmykConverter Instance = new DeviceCmykConverter();
    private static readonly IccProfileTransform _iccTransform;

    static DeviceCmykConverter()
    {
        var cmykIccBytes = PdfResourceLoader.GetResource("CompactCmyk.icc");
        var cmykProfile = IccProfile.Parse(cmykIccBytes);
        _iccTransform = new IccProfileTransform(cmykProfile);
    }

    public override int Components => 4;

    public override bool IsDevice => true;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        return new ColorTransformSampler(_iccTransform.GetIntentTransform(intent));
    }
}
