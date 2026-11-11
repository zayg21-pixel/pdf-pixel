using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Converts float samples in the range 0 to 1 to integer samples of 8 or 16 bits, rounded to nearest and clamped; 16-bit
/// samples are stored most significant byte first.
/// </summary>
public sealed class PdfImageRowFloatToByteStep : IPdfImageRowProcessingStep
{
    private const float MaximumByteValue = byte.MaxValue;
    private const float MaximumShortValue = ushort.MaxValue;
    private const float RoundingOffset = 0.5f;
    private const int ByteBitsPerComponent = 8;
    private const int ShortBitsPerComponent = 16;

    private readonly int _bitsPerComponent;

    /// <summary>
    /// Initializes a step converting to samples of <paramref name="bitsPerComponent"/> bits.
    /// </summary>
    /// <param name="bitsPerComponent">Bit depth of each output sample: 8 or 16.</param>
    public PdfImageRowFloatToByteStep(int bitsPerComponent)
    {
        if (bitsPerComponent != ByteBitsPerComponent && bitsPerComponent != ShortBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Bits per component must be 8 or 16.");
        }

        _bitsPerComponent = bitsPerComponent;
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => inputLength / sizeof(float) * (_bitsPerComponent / ByteBitsPerComponent);

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
        ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(input);

        if (_bitsPerComponent == ByteBitsPerComponent)
        {
            ConvertSamples(samples, output);
        }
        else
        {
            ConvertShortSamples(samples, output);
        }
    }

    /// <summary>
    /// Writes each sample as two bytes, most significant first, from the first to the last; a sample is read before its
    /// narrower output is written, so the output may start where the input does.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ConvertShortSamples(in ReadOnlySpan<float> source, in Span<byte> destination)
    {
        int sampleCount = source.Length;
        ref float sourceReference = ref MemoryMarshal.GetReference(source);
        ref byte destinationReference = ref MemoryMarshal.GetReference(destination);

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float scaled = (Unsafe.Add(ref sourceReference, sampleIndex) * MaximumShortValue) + RoundingOffset;
            var value = (ushort)Math.Min(Math.Max(scaled, 0f), MaximumShortValue);

            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }

            Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationReference, sampleIndex * sizeof(ushort)), value);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ConvertSamples(in ReadOnlySpan<float> source, in Span<byte> destination)
    {
        int sampleCount = source.Length;
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
