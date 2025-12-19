using PdfReader.Color.Structures;
using PdfReader.Functions;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

internal sealed class SingleChannelFunctionSampler : IRgbaSampler
{
    private readonly RgbaPacked[] _tintLut;
    private const int DefaultLutSize = 256;

    public SingleChannelFunctionSampler(PdfFunction tintFunction, IRgbaSampler alternateSampler)
    {
        _tintLut = BuildTintLut(tintFunction, alternateSampler, DefaultLutSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        if (source.Length == 0)
        {
            return;
        }

#if NET5_0_OR_GREATER
        int index = (int)float.FusedMultiplyAdd(source[0], DefaultLutSize - 1, 0.5f);
        index = Math.Clamp(index, 0, DefaultLutSize - 1);
        ref var result = ref _tintLut[index];
#else
        int index = (int)(source[0] * (DefaultLutSize - 1) + 0.5f); // Round to nearest
        index = Math.Max(0, Math.Min(DefaultLutSize - 1, index));
        ref var result = ref _tintLut[index];
#endif

        ref byte destinationBytes = ref Unsafe.As<RgbaPacked, byte>(ref destination);
        Unsafe.WriteUnaligned(ref destinationBytes, result);
    }

    /// <summary>
    /// Builds a tint LUT with pre-computed RgbaPacked results for 0-1 range.
    /// </summary>
    private static RgbaPacked[] BuildTintLut(PdfFunction tintFunction, IRgbaSampler alternateSampler, int lutSize)
    {
        var lut = new RgbaPacked[lutSize];

        for (int i = 0; i < lutSize; i++)
        {
            float tint = (float)i / (lutSize - 1);
            var mappedComponents = tintFunction.Evaluate(tint);

            // Pre-compute the final RgbaPacked value
            alternateSampler.Sample(mappedComponents, ref lut[i]);
        }

        return lut;
    }
}
