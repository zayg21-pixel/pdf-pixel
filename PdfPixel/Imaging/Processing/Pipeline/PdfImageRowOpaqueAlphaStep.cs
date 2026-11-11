using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Appends an opaque alpha channel to 8 or 16-bit integer pixels or float pixels: each pixel of the input channels is
/// followed by the largest value of its format, or 1 for floats.
/// </summary>
public sealed class PdfImageRowOpaqueAlphaStep : IPdfImageRowProcessingStep
{
    private const int ByteBitsPerComponent = 8;
    private const int ShortBitsPerComponent = 16;
    private const int FloatBitsPerComponent = 32;

    private readonly int _channelCount;
    private readonly int _sampleBytes;

    /// <summary>
    /// Initializes a step for pixels of <paramref name="channelCount"/> channels.
    /// </summary>
    /// <param name="channelCount">Number of channels in each input pixel.</param>
    /// <param name="format">How each sample is stored.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample: 8 or 16 for integers, 32 for floats.</param>
    public PdfImageRowOpaqueAlphaStep(int channelCount, PdfImageSampleFormat format, int bitsPerComponent)
    {
        if (channelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount), "Channel count must be positive.");
        }

        if (format == PdfImageSampleFormat.Float && bitsPerComponent != FloatBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Float samples must be 32 bits per component.");
        }

        if (format == PdfImageSampleFormat.Integer && bitsPerComponent != ByteBitsPerComponent && bitsPerComponent != ShortBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Integer samples must be 8 or 16 bits per component.");
        }

        _channelCount = channelCount;
        _sampleBytes = bitsPerComponent / ByteBitsPerComponent;
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => inputLength / (_channelCount * _sampleBytes) * (_channelCount + 1) * _sampleBytes;

    /// <inheritdoc />
    public PdfImageOutputRow GetOutputRow(int inputRowIndex) => new(inputRowIndex, hasNextRow: false);

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output, int inputRowIndex) => Process(input, output);

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void Process(ReadOnlySpan<byte> input, Span<byte> output)
    {
        int pixelCount = input.Length / (_channelCount * _sampleBytes);
        ref byte source = ref MemoryMarshal.GetReference(input);
        ref byte destination = ref MemoryMarshal.GetReference(output);

        if (_sampleBytes == sizeof(byte))
        {
            AppendAlphaFromEnd(ref source, ref destination, pixelCount, _channelCount, byte.MaxValue);
        }
        else if (_sampleBytes == sizeof(ushort))
        {
            AppendAlphaFromEnd(ref Unsafe.As<byte, ushort>(ref source), ref Unsafe.As<byte, ushort>(ref destination), pixelCount, _channelCount, ushort.MaxValue);
        }
        else
        {
            AppendAlphaFromEnd(ref Unsafe.As<byte, float>(ref source), ref Unsafe.As<byte, float>(ref destination), pixelCount, _channelCount, 1f);
        }
    }

    /// <summary>
    /// Writes the pixels from the last to the first, so that the wider output pixels never reach an input pixel still to
    /// be read in the row they may share.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void AppendAlphaFromEnd<TSample>(ref TSample source, ref TSample destination, int pixelCount, int channelCount, TSample opaque)
        where TSample : unmanaged
    {
        int outputChannelCount = channelCount + 1;
        int inputOffset = (pixelCount - 1) * channelCount;
        int outputOffset = (pixelCount - 1) * outputChannelCount;

        for (int x = pixelCount - 1; x >= 0; x--)
        {
            ref TSample inputPixel = ref Unsafe.Add(ref source, inputOffset);
            ref TSample outputPixel = ref Unsafe.Add(ref destination, outputOffset);

            Unsafe.Add(ref outputPixel, channelCount) = opaque;

            for (int channel = channelCount - 1; channel >= 0; channel--)
            {
                Unsafe.Add(ref outputPixel, channel) = Unsafe.Add(ref inputPixel, channel);
            }

            inputOffset -= channelCount;
            outputOffset -= outputChannelCount;
        }
    }
}
