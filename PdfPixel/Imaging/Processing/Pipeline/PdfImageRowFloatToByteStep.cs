using PdfPixel.Parsing;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Converts float samples in the range 0 to 1 to packed integer samples of 1, 2, 4, 8 or 16 bits,
/// rounded to nearest and clamped.
/// </summary>
public sealed class PdfImageRowFloatToByteStep : IPdfImageRowProcessingStep
{
    private const float MaximumByteValue = byte.MaxValue;
    private const float RoundingOffset = 0.5f;
    private const int ByteBitsPerComponent = 8;

    private readonly int _bitsPerComponent;
    private readonly int _sampleCount;
    private readonly byte[] _buffer;

    /// <summary>
    /// Initializes a step converting rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="componentCount">Number of components in each pixel.</param>
    /// <param name="bitsPerComponent">Bit depth of each output sample: 1, 2, 4, 8 or 16.</param>
    public PdfImageRowFloatToByteStep(int width, int componentCount, int bitsPerComponent)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (componentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be positive.");
        }

        if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8 && bitsPerComponent != 16)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Bits per component must be 1, 2, 4, 8 or 16.");
        }

        _bitsPerComponent = bitsPerComponent;
        _sampleCount = width * componentCount;
        _buffer = new byte[((_sampleCount * bitsPerComponent) + 7) / 8];
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfImageRow Process(ReadOnlySpan<byte> input, int inputRowIndex)
    {
        ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(input);

        if (samples.Length < _sampleCount)
        {
            throw new ArgumentException("The input row holds fewer samples than the step converts.", nameof(input));
        }

        if (_bitsPerComponent == ByteBitsPerComponent)
        {
            ConvertSamples(samples, _buffer);
        }
        else
        {
            PackSamples(samples);
        }

        return new PdfImageRow(_buffer, inputRowIndex, hasNextRow: false);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void PackSamples(in ReadOnlySpan<float> source)
    {
        float maximumCode = (1 << _bitsPerComponent) - 1;
        ref float sourceReference = ref MemoryMarshal.GetReference(source);
        UintBitWriter writer = new(_buffer);

        for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
        {
            float scaled = (Unsafe.Add(ref sourceReference, sampleIndex) * maximumCode) + RoundingOffset;
            writer.WriteBits(_bitsPerComponent, (uint)Math.Min(Math.Max(scaled, 0f), maximumCode));
        }

        writer.Flush();
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ConvertSamples(in ReadOnlySpan<float> source, in Span<byte> destination)
    {
        int sampleCount = destination.Length;
        ref float sourceReference = ref MemoryMarshal.GetReference(source);
        ref byte destinationReference = ref MemoryMarshal.GetReference(destination);
        int sampleIndex = 0;

        if (Vector.IsHardwareAccelerated && sampleCount >= Vector<byte>.Count)
        {
            int floatCount = Vector<float>.Count;
            int lastBlock = sampleCount - Vector<byte>.Count;

            for (; sampleIndex <= lastBlock; sampleIndex += Vector<byte>.Count)
            {
                Vector<uint> first = ConvertVector(ref Unsafe.Add(ref sourceReference, sampleIndex));
                Vector<uint> second = ConvertVector(ref Unsafe.Add(ref sourceReference, sampleIndex + floatCount));
                Vector<uint> third = ConvertVector(ref Unsafe.Add(ref sourceReference, sampleIndex + (floatCount * 2)));
                Vector<uint> fourth = ConvertVector(ref Unsafe.Add(ref sourceReference, sampleIndex + (floatCount * 3)));

                Vector<byte> converted = Vector.Narrow(Vector.Narrow(first, second), Vector.Narrow(third, fourth));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationReference, sampleIndex), converted);
            }
        }

        for (; sampleIndex < sampleCount; sampleIndex++)
        {
            float scaled = (Unsafe.Add(ref sourceReference, sampleIndex) * MaximumByteValue) + RoundingOffset;
            Unsafe.Add(ref destinationReference, sampleIndex) = (byte)Math.Min(Math.Max(scaled, 0f), MaximumByteValue);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> ConvertVector(ref float source)
    {
        Vector<float> samples = Unsafe.ReadUnaligned<Vector<float>>(ref Unsafe.As<float, byte>(ref source));
        Vector<float> scaled = (samples * new Vector<float>(MaximumByteValue)) + new Vector<float>(RoundingOffset);
        Vector<float> clamped = Vector.Min(Vector.Max(scaled, Vector<float>.Zero), new Vector<float>(MaximumByteValue));

        return Vector.AsVectorUInt32(Vector.ConvertToInt32(clamped));
    }
}
