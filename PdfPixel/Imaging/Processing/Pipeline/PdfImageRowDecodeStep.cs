using PdfPixel.Objects;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Maps float samples in the range 0 to 1 onto the ranges of a /Decode array (ISO 32000-2 8.9.5.2), one range per
/// component; channels after the components are left as they are.
/// </summary>
public sealed class PdfImageRowDecodeStep : IPdfImageRowProcessingStep
{
    private readonly float[] _minimum;
    private readonly float[] _scale;
    private readonly int _channelCount;

    /// <summary>
    /// Initializes a step mapping components onto <paramref name="decode"/>.
    /// </summary>
    /// <param name="decode">The range of each component.</param>
    /// <param name="channelCount">The number of channels of a pixel: the components, then any others.</param>
    public PdfImageRowDecodeStep(PdfRange[] decode, int channelCount)
    {
        if (decode == null || decode.Length == 0)
        {
            throw new ArgumentException("At least one decode range is required.", nameof(decode));
        }

        if (channelCount < decode.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount), "A pixel holds fewer channels than decode ranges.");
        }

        _channelCount = channelCount;
        _minimum = new float[decode.Length];
        _scale = new float[decode.Length];

        for (int component = 0; component < decode.Length; component++)
        {
            _minimum[component] = decode[component].Min;
            _scale[component] = decode[component].Max - decode[component].Min;
        }
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
        int componentCount = _minimum.Length;
        int width = samples.Length / _channelCount;
        ref float sample = ref MemoryMarshal.GetReference(samples);
        ref float destination = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(output));
        ref float minimum = ref _minimum[0];
        ref float scale = ref _scale[0];

        for (int x = 0; x < width; x++)
        {
            for (int component = 0; component < componentCount; component++)
            {
                Unsafe.Add(ref destination, component) = Unsafe.Add(ref minimum, component) + (Unsafe.Add(ref sample, component) * Unsafe.Add(ref scale, component));
            }

            for (int channel = componentCount; channel < _channelCount; channel++)
            {
                Unsafe.Add(ref destination, channel) = Unsafe.Add(ref sample, channel);
            }

            sample = ref Unsafe.Add(ref sample, _channelCount);
            destination = ref Unsafe.Add(ref destination, _channelCount);
        }
    }
}
