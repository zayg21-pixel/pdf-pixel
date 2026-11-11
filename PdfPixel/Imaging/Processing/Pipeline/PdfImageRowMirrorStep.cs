using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Reverses the order of the pixels of float sample rows.
/// </summary>
public sealed class PdfImageRowMirrorStep : IPdfImageRowProcessingStep
{
    private readonly int _channelCount;

    /// <summary>
    /// Initializes a step for pixels of <paramref name="channelCount"/> channels.
    /// </summary>
    public PdfImageRowMirrorStep(int channelCount)
    {
        if (channelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount), "Channel count must be positive.");
        }

        _channelCount = channelCount;
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => inputLength;

    /// <inheritdoc />
    public PdfImageOutputRow GetOutputRow(int inputRowIndex) => new(inputRowIndex, hasNextRow: false);

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output, int inputRowIndex) => Process(input, output);

    /// <inheritdoc />
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public void Process(ReadOnlySpan<byte> input, Span<byte> output)
    {
        ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(input);
        int width = samples.Length / _channelCount;
        ref float sample = ref MemoryMarshal.GetReference(samples);
        ref float destination = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(output));

        // Each pixel swaps with its mirror, both read before either is written, so the output may start where the
        // input does.
        ref float leftSample = ref sample;
        ref float rightSample = ref Unsafe.Add(ref sample, (width - 1) * _channelCount);
        ref float leftDestination = ref destination;
        ref float rightDestination = ref Unsafe.Add(ref destination, (width - 1) * _channelCount);

        for (int x = 0; x < (width + 1) / 2; x++)
        {
            for (int channel = 0; channel < _channelCount; channel++)
            {
                float left = Unsafe.Add(ref leftSample, channel);
                float right = Unsafe.Add(ref rightSample, channel);
                Unsafe.Add(ref leftDestination, channel) = right;
                Unsafe.Add(ref rightDestination, channel) = left;
            }

            leftSample = ref Unsafe.Add(ref leftSample, _channelCount);
            rightSample = ref Unsafe.Add(ref rightSample, -_channelCount);
            leftDestination = ref Unsafe.Add(ref leftDestination, _channelCount);
            rightDestination = ref Unsafe.Add(ref rightDestination, -_channelCount);
        }
    }
}
