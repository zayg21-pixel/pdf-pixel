using PdfReader.Color.Structures;
using PdfReader.Color.Transform;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

internal sealed class ColorTransformSampler : IRgbaSampler
{
    private readonly ChainedColorTransform _colorTransform;

    public ColorTransformSampler(ChainedColorTransform chainedTransform)
    {
        _colorTransform = chainedTransform;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        var value = ColorVectorUtilities.ToVector4WithOnePadding(source);

        value = _colorTransform.Transform(value);

        ColorVectorUtilities.Load01ToRgba(value, ref destination);
    }
}
