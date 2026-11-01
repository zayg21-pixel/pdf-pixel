using PdfPixel.Color.Transform;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The CalGray color space: a single gray component defined by CIE colorimetry rather than by the
/// output device.
/// </summary>
public sealed class PdfCalibratedGrayColorSpaceConverter : PdfCalibratedColorSpaceConverter
{
    internal PdfCalibratedGrayColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float? gamma)
        : this(GetWhitePointOrDefault(whitePoint), GetBlackPointOrDefault(blackPoint), gamma ?? 1f)
    {
    }

    private PdfCalibratedGrayColorSpaceConverter(float[] whitePoint, float[] blackPoint, float gamma)
        : base(whitePoint, blackPoint, [gamma, gamma, gamma], [whitePoint[0], 0f, 0f, 0f, whitePoint[1], 0f, 0f, 0f, whitePoint[2]])
    {
        Gamma = gamma;
    }

    /// <summary>
    /// Gets the gamma of the gray component (/Gamma), or 1 when absent.
    /// </summary>
    public float Gamma { get; }

    /// <inheritdoc />
    public override int Components => 1;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(in PdfColorConversionParameters parameters, bool normalize)
    {
        ChainedColorTransform toGrayChain = new(
            new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1f)),
            GetToSrgbTransform(parameters.RenderingIntent),
            parameters.TransferFunction,
            new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1f)));
        return new ColorTransformSampler(toGrayChain);
    }
}
