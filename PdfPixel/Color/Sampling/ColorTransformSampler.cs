using PdfPixel.Color.Transform;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Sampling;

internal sealed class ColorTransformSampler : IRgbaSampler
{
    private readonly ChainedColorTransform _colorTransform;

    public ColorTransformSampler(ChainedColorTransform chainedTransform)
    {
        _colorTransform = chainedTransform;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Sample(ReadOnlySpan<float> source)
    {
        var value = ColorVectorUtilities.ToVector4WithOnePadding(source);
        return _colorTransform.Transform(value);

    }
}
