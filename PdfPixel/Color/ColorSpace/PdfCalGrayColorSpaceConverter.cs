using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

// TODO: all of color spaces are lacking documentation
internal sealed class PdfCalGrayColorSpaceConverter : PdfCalRgbColorSpaceConverter
{
    public PdfCalGrayColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float? gamma)
        : base(whitePoint, blackPoint, (gamma.HasValue) ? [gamma.Value, gamma.Value, gamma.Value] : null, null)
    {
    }

    public override int Components => 1;

    public override bool IsDevice => false;

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
    {
        ChainedColorTransform toGrayChain = new(
            new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1f)),
            ToSrgbTransform,
            postTransform,
            new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1f)));
        return new ColorTransformSampler(toGrayChain);
    }
}
