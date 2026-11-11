using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Divides the components of each pixel, 8 or 16-bit integers or floats, by the alpha channel that follows them;
/// integers are rounded to the nearest value and limited to the largest one, and pixels with zero alpha get zero
/// components.
/// </summary>
public sealed class PdfImageRowUnpremultiplyStep : IPdfImageRowProcessingStep
{
    private const int ByteBitsPerComponent = 8;
    private const int ShortBitsPerComponent = 16;
    private const int FloatBitsPerComponent = 32;

    private readonly int _componentCount;
    private readonly int _sampleBytes;

    /// <summary>
    /// Initializes a step for pixels of <paramref name="componentCount"/> components followed by alpha.
    /// </summary>
    /// <param name="componentCount">Number of components before the alpha of each pixel.</param>
    /// <param name="format">How each sample is stored.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample: 8 or 16 for integers, 32 for floats.</param>
    public PdfImageRowUnpremultiplyStep(int componentCount, PdfImageSampleFormat format, int bitsPerComponent)
    {
        if (componentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be positive.");
        }

        if (format == PdfImageSampleFormat.Float && bitsPerComponent != FloatBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Float samples must be 32 bits per component.");
        }

        if (format == PdfImageSampleFormat.Integer && bitsPerComponent != ByteBitsPerComponent && bitsPerComponent != ShortBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Integer samples must be 8 or 16 bits per component.");
        }

        _componentCount = componentCount;
        _sampleBytes = bitsPerComponent / ByteBitsPerComponent;
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => inputLength;

    /// <inheritdoc />
    public PdfImageOutputRow GetOutputRow(int inputRowIndex) => new(inputRowIndex, hasNextRow: false);

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output, int inputRowIndex) => Process(input, output);

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (_sampleBytes == sizeof(byte))
        {
            UnpremultiplyBytes(input, output);
        }
        else if (_sampleBytes == sizeof(ushort))
        {
            UnpremultiplyShorts(MemoryMarshal.Cast<byte, ushort>(input), MemoryMarshal.Cast<byte, ushort>(output));
        }
        else
        {
            UnpremultiplyFloats(MemoryMarshal.Cast<byte, float>(input), MemoryMarshal.Cast<byte, float>(output));
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpremultiplyBytes(in ReadOnlySpan<byte> samples, in Span<byte> output)
    {
        int stride = _componentCount + 1;
        int width = samples.Length / stride;
        ref byte sample = ref MemoryMarshal.GetReference(samples);
        ref byte destination = ref MemoryMarshal.GetReference(output);

        for (int x = 0; x < width; x++)
        {
            uint alpha = Unsafe.Add(ref sample, _componentCount);

            for (int component = 0; component < _componentCount; component++)
            {
                uint value = 0;

                if (alpha != 0)
                {
                    value = Math.Min(byte.MaxValue, ((Unsafe.Add(ref sample, component) * (uint)byte.MaxValue) + (alpha / 2)) / alpha);
                }

                Unsafe.Add(ref destination, component) = (byte)value;
            }

            Unsafe.Add(ref destination, _componentCount) = (byte)alpha;
            sample = ref Unsafe.Add(ref sample, stride);
            destination = ref Unsafe.Add(ref destination, stride);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpremultiplyShorts(in ReadOnlySpan<ushort> samples, in Span<ushort> output)
    {
        int stride = _componentCount + 1;
        int width = samples.Length / stride;
        ref ushort sample = ref MemoryMarshal.GetReference(samples);
        ref ushort destination = ref MemoryMarshal.GetReference(output);

        for (int x = 0; x < width; x++)
        {
            uint alpha = Unsafe.Add(ref sample, _componentCount);

            for (int component = 0; component < _componentCount; component++)
            {
                uint value = 0;

                if (alpha != 0)
                {
                    value = Math.Min(ushort.MaxValue, ((Unsafe.Add(ref sample, component) * (uint)ushort.MaxValue) + (alpha / 2)) / alpha);
                }

                Unsafe.Add(ref destination, component) = (ushort)value;
            }

            Unsafe.Add(ref destination, _componentCount) = (ushort)alpha;
            sample = ref Unsafe.Add(ref sample, stride);
            destination = ref Unsafe.Add(ref destination, stride);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpremultiplyFloats(in ReadOnlySpan<float> samples, in Span<float> output)
    {
        int stride = _componentCount + 1;
        int width = samples.Length / stride;
        ref float sample = ref MemoryMarshal.GetReference(samples);
        ref float destination = ref MemoryMarshal.GetReference(output);

        for (int x = 0; x < width; x++)
        {
            float alpha = Unsafe.Add(ref sample, _componentCount);
            float inverseAlpha = (alpha > 0) ? 1 / alpha : 0;

            for (int component = 0; component < _componentCount; component++)
            {
                Unsafe.Add(ref destination, component) = Unsafe.Add(ref sample, component) * inverseAlpha;
            }

            Unsafe.Add(ref destination, _componentCount) = alpha;
            sample = ref Unsafe.Add(ref sample, stride);
            destination = ref Unsafe.Add(ref destination, stride);
        }
    }
}
