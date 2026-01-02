using PdfReader.Functions;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

delegate ReadOnlySpan<float> FunctionSampleProcessor(ReadOnlySpan<float> source);

internal sealed class FunctionSampler : IRgbaSampler
{
    private readonly PdfFunction _tintFunction;
    private readonly IRgbaSampler _alternateSampler;
    private readonly FunctionSampleProcessor _functionCallback;


    public FunctionSampler(PdfFunction tintFunction, IRgbaSampler alternateSampler)
    {
        _tintFunction = tintFunction;
        _alternateSampler = alternateSampler;
        _functionCallback = _tintFunction.Evaluate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Sample(ReadOnlySpan<float> source)
    {
        ReadOnlySpan<float> mapped = _functionCallback(source);
        return _alternateSampler.Sample(mapped);
    }
}
