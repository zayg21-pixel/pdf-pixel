using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Picks and reorders the channels of 8 or 16-bit integer samples or float samples: each output channel is a copy of the
/// input channel at its index. Every pixel a row holds is mapped, whatever its length.
/// </summary>
public sealed class PdfImageRowChannelMapStep : IPdfImageRowProcessingStep
{
    private const int ByteBitsPerComponent = 8;
    private const int ShortBitsPerComponent = 16;
    private const int FloatBitsPerComponent = 32;

    private readonly int _inputChannelCount;
    private readonly int[] _inputChannels;
    private readonly int _sampleBytes;

    /// <summary>
    /// Initializes a step mapping pixels of <paramref name="inputChannelCount"/> channels onto
    /// <paramref name="inputChannels"/>.
    /// </summary>
    /// <param name="inputChannelCount">Number of channels in each input pixel.</param>
    /// <param name="inputChannels">
    /// For each output channel, the index of the input channel it copies; at most <paramref name="inputChannelCount"/>
    /// of them.
    /// </param>
    /// <param name="format">How each sample is stored.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample: 8 or 16 for integers, 32 for floats.</param>
    public PdfImageRowChannelMapStep(int inputChannelCount, int[] inputChannels, PdfImageSampleFormat format, int bitsPerComponent)
    {
        if (inputChannelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputChannelCount), "Input channel count must be positive.");
        }

        if (inputChannels == null || inputChannels.Length == 0 || inputChannels.Length > inputChannelCount)
        {
            throw new ArgumentException("Between one and the input channel count of channels are required.", nameof(inputChannels));
        }

        foreach (int inputChannel in inputChannels)
        {
            if (inputChannel < 0 || inputChannel >= inputChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(inputChannels), "Every channel must index an input channel.");
            }
        }

        if (format == PdfImageSampleFormat.Float && bitsPerComponent != FloatBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Float samples must be 32 bits per component.");
        }

        if (format == PdfImageSampleFormat.Integer && bitsPerComponent != ByteBitsPerComponent && bitsPerComponent != ShortBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Integer samples must be 8 or 16 bits per component.");
        }

        _inputChannelCount = inputChannelCount;
        _inputChannels = inputChannels;
        _sampleBytes = bitsPerComponent / ByteBitsPerComponent;
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => GetPixelCount(inputLength) * _inputChannels.Length * _sampleBytes;

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
        int pixelCount = GetPixelCount(input.Length);

        if (output.Length < pixelCount * _inputChannels.Length * _sampleBytes)
        {
            throw new ArgumentException("The output row is shorter than the mapped pixels.", nameof(output));
        }

        ref byte inputReference = ref MemoryMarshal.GetReference(input);
        ref byte outputReference = ref MemoryMarshal.GetReference(output);

        if (_sampleBytes == sizeof(byte))
        {
            Map(ref inputReference, ref outputReference, pixelCount);
        }
        else if (_sampleBytes == sizeof(ushort))
        {
            Map(ref Unsafe.As<byte, ushort>(ref inputReference), ref Unsafe.As<byte, ushort>(ref outputReference), pixelCount);
        }
        else
        {
            Map(ref Unsafe.As<byte, uint>(ref inputReference), ref Unsafe.As<byte, uint>(ref outputReference), pixelCount);
        }
    }

    private int GetPixelCount(int inputLength) => inputLength / (_inputChannelCount * _sampleBytes);

    /// <summary>
    /// Maps the pixels from the first to the last; each pixel is read before it is written and is never wider than its
    /// input, so the output may start where the input does.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void Map<TSample>(ref TSample input, ref TSample output, int pixelCount)
        where TSample : unmanaged
    {
        int outputChannelCount = _inputChannels.Length;
        ref int inputChannels = ref MemoryMarshal.GetReference(_inputChannels.AsSpan());
        Span<TSample> pixel = stackalloc TSample[_inputChannelCount];
        ref TSample pixelReference = ref MemoryMarshal.GetReference(pixel);
        int inputOffset = 0;
        int outputOffset = 0;

        for (int x = 0; x < pixelCount; x++)
        {
            ref TSample inputPixel = ref Unsafe.Add(ref input, inputOffset);
            ref TSample outputPixel = ref Unsafe.Add(ref output, outputOffset);

            inputOffset += _inputChannelCount;
            outputOffset += outputChannelCount;

            for (int channel = 0; channel < _inputChannelCount; channel++)
            {
                Unsafe.Add(ref pixelReference, channel) = Unsafe.Add(ref inputPixel, channel);
            }

            for (int channel = 0; channel < outputChannelCount; channel++)
            {
                Unsafe.Add(ref outputPixel, channel) = Unsafe.Add(ref pixelReference, Unsafe.Add(ref inputChannels, channel));
            }
        }
    }
}
