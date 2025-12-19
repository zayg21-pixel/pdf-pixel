using PdfReader.Color.Structures;
using PdfReader.Functions;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

internal sealed class FunctionSampler : IRgbaSampler
{
    private readonly PdfFunction _tintFunction;
    private readonly IRgbaSampler _alternateSampler;

    public FunctionSampler(PdfFunction tintFunction, IRgbaSampler alternateSampler)
    {
        _tintFunction = tintFunction;
        _alternateSampler = alternateSampler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        ReadOnlySpan<float> mapped = _tintFunction.Evaluate(source);
        _alternateSampler.Sample(mapped, ref destination);

    }
}
