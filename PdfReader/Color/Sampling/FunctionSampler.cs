using PdfReader.Color.Structures;
using PdfReader.Functions;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

delegate void SampleProcessor(ReadOnlySpan<float> source, ref RgbaPacked destination);

delegate ReadOnlySpan<float> FunctionSampleProcessor(ReadOnlySpan<float> source);

internal sealed class FunctionSampler : IRgbaSampler
{
    private readonly PdfFunction _tintFunction;
    private readonly IRgbaSampler _alternateSampler;
    private readonly SampleProcessor _processorCallback;
    private readonly FunctionSampleProcessor _functionCallback;


    public FunctionSampler(PdfFunction tintFunction, IRgbaSampler alternateSampler)
    {
        _tintFunction = tintFunction;
        _alternateSampler = alternateSampler;
        _processorCallback = _alternateSampler.Sample;
        _functionCallback = _tintFunction.Evaluate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        ReadOnlySpan<float> mapped = _functionCallback(source);
        _processorCallback(mapped, ref destination);
    }
}
