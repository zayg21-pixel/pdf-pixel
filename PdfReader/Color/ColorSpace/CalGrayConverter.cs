using PdfReader.Color.Icc.Transform;
using PdfReader.Color.Sampling;
using PdfReader.Color.Transform;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// <see cref="CalRgbConverter"/> based converter for CalGray (CIEBasedGray) color space.
/// </summary>
internal sealed class CalGrayConverter : CalRgbConverter
{
    public CalGrayConverter(float[] whitePoint, float[] blackPoint, float? gamma)
        : base(whitePoint, blackPoint, gamma.HasValue ? [gamma.Value, gamma.Value, gamma.Value] : null, null)
    {
    }

    public override int Components => 1;

    public override bool IsDevice => false;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        var toGrayChain = new ChainedColorTransform(new FunctionColorTransform((ref Vector4 x) => x = new Vector4(x.X, x.X, x.X, 1f)), ToSrgbTransform);
        return new ColorTransformSampler(toGrayChain);
    }
}
