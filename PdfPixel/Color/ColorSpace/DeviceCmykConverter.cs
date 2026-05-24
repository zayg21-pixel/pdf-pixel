using PdfPixel.Color.Icc;
using PdfPixel.Color.Profiles;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using System;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

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
        var cmykProfile = ProfileRespources.GetCmykProfile();
        _iccTransform = new IccProfileTransform(cmykProfile);
    }

    public override int Components => 4;

    public override bool IsDevice => true;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        return new ColorTransformSampler(new ChainedColorTransform(_iccTransform.GetIntentTransform(intent.ToIccRenderingIntent()), postTransform));
    }
}