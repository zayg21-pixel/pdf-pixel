using PdfPixel.Color.Icc;
using PdfPixel.Color.Profiles;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The DeviceCMYK color space: four components giving the cyan, magenta, yellow, and black
/// subtractive colorants of the output device.
/// </summary>
public sealed class PdfDeviceCmykColorSpaceConverter : PdfColorSpaceConverter
{
    /// <summary>
    /// The shared instance of this color space.
    /// </summary>
    public static readonly PdfDeviceCmykColorSpaceConverter Instance = new();

    private static readonly IccProfileTransform _iccTransform;

    static PdfDeviceCmykColorSpaceConverter()
    {
        Icc.Model.IccProfile cmykProfile = ProfileRespources.GetCmykProfile();
        _iccTransform = new IccProfileTransform(cmykProfile);
    }

    /// <inheritdoc />
    public override int Components => 4;

    /// <inheritdoc />
    public override bool IsDevice => true;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
        => new(new ChainedColorTransform(_iccTransform.GetIntentTransform(intent.ToIccRenderingIntent()), postTransform));
}
