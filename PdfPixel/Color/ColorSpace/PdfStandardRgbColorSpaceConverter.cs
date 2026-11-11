using PdfPixel.Color.Icc;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The sRGB color space, the built-in characterization of DeviceRGB.
/// </summary>
internal sealed class PdfStandardRgbColorSpaceConverter : PdfColorSpaceConverter
{
    public static readonly PdfStandardRgbColorSpaceConverter Instance = new();

    private PdfStandardRgbColorSpaceConverter()
    {
    }

    /// <inheritdoc />
    public override int Components => 3;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    public override ChainedColorTransform GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
        => IccTransforms.SrgbToXyzD50Transform;

    /// <inheritdoc />
    public override ChainedColorTransform GetFromProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
        => IccTransforms.XyzD50ToSrgbTransform;
}
