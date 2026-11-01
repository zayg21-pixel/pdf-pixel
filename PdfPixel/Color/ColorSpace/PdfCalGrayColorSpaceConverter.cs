using PdfPixel.Color.Icc;
using PdfPixel.Color.Transform;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The CalGray color space: a single gray component defined by CIE colorimetry rather than by the
/// output device.
/// </summary>
public sealed class PdfCalGrayColorSpaceConverter : PdfCalRgbColorSpaceConverter
{
    /// <summary>
    /// Initializes the space from its white point, black point, and gamma.
    /// </summary>
    public PdfCalGrayColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float? gamma)
        : base(whitePoint, blackPoint, (gamma.HasValue) ? [gamma.Value, gamma.Value, gamma.Value] : null, BuildWhitePointMatrix(whitePoint))
    {
    }

    /// <inheritdoc />
    public override int Components => 1;

    /// <inheritdoc />
    public override bool IsDevice => false;

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

    private static float[,] BuildWhitePointMatrix(float[]? whitePoint)
    {
        Vector4 whitePointVector = IccTransforms.D65WhitePoint;

        if (whitePoint?.Length >= 3)
        {
            whitePointVector = ColorVectorUtilities.ToVector4WithOnePadding(whitePoint);
        }

        return new float[3, 3]
        {
            { whitePointVector.X, 0, 0 },
            { 0, whitePointVector.Y, 0 },
            { 0, 0, whitePointVector.Z }
        };
    }
}
