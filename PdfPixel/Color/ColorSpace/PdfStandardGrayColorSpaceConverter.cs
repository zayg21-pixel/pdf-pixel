using PdfPixel.Color.Icc;
using PdfPixel.Color.Transform;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The sRGB gray, (g, g, g) in sRGB: the built-in characterization of DeviceGray.
/// </summary>
internal sealed class PdfStandardGrayColorSpaceConverter : PdfColorSpaceConverter
{
    public static readonly PdfStandardGrayColorSpaceConverter Instance = new();

    private readonly ChainedColorTransform _toXyzTransform = new(new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1f)), IccTransforms.SrgbToXyzD50Transform);

    private PdfStandardGrayColorSpaceConverter()
    {
    }

    /// <inheritdoc />
    public override int Components => 1;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    public override ChainedColorTransform GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
        => _toXyzTransform;

    /// <inheritdoc />
    public override ChainedColorTransform GetFromProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
        => IccTransforms.XyzD50ToSrgbGrayTransform;
}
