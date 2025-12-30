using PdfReader.Color.Structures;
using PdfReader.Color.Transform;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

internal sealed class ColorTransformSampler : IRgbaSampler
{
    private readonly ChainedColorTransform _colorTransform;
    private readonly PixelProcessorFunction _processorFunction;

    public ColorTransformSampler(ChainedColorTransform chainedTransform)
    {
        _colorTransform = chainedTransform;
        _processorFunction = chainedTransform.GetTransformCallback();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        var value = ColorVectorUtilities.ToVector4WithOnePadding(source);

        value = _processorFunction(value);

        ColorVectorUtilities.Load01ToRgba(value, ref destination);
    }
}
