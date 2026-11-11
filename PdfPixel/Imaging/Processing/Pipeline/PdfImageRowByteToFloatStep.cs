using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Converts integer samples of 8 or 16 bits to float samples in the range 0 to 1; 16-bit samples are read most
/// significant byte first.
/// </summary>
public sealed class PdfImageRowByteToFloatStep : IPdfImageRowProcessingStep
{
    private const int ByteBitsPerComponent = 8;
    private const int ShortBitsPerComponent = 16;

    private readonly int _sampleBytes;
    private readonly float _scale;

    /// <summary>
    /// Initializes a step converting samples of <paramref name="bitsPerComponent"/> bits.
    /// </summary>
    /// <param name="bitsPerComponent">Bit depth of each input sample: 8 or 16.</param>
    public PdfImageRowByteToFloatStep(int bitsPerComponent)
    {
        if (bitsPerComponent != ByteBitsPerComponent && bitsPerComponent != ShortBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Bits per component must be 8 or 16.");
        }

        _sampleBytes = bitsPerComponent / ByteBitsPerComponent;
        _scale = 1f / ((1 << bitsPerComponent) - 1);
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => inputLength / _sampleBytes * sizeof(float);

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
        int sampleCount = input.Length / _sampleBytes;
        ref byte inputReference = ref MemoryMarshal.GetReference(input);
        ref float sampleReference = ref Unsafe.As<byte, float>(ref MemoryMarshal.GetReference(output));
        bool inPlace = Unsafe.AreSame(ref inputReference, ref MemoryMarshal.GetReference(output));

        if (_sampleBytes == sizeof(byte) && inPlace)
        {
            ConvertBytesFromEnd(ref inputReference, ref sampleReference, sampleCount, _scale);
        }
        else if (_sampleBytes == sizeof(byte))
        {
            ConvertBytesFromStart(ref inputReference, ref sampleReference, sampleCount, _scale);
        }
        else if (inPlace)
        {
            ConvertShortsFromEnd(ref inputReference, ref sampleReference, sampleCount, _scale);
        }
        else
        {
            ConvertShortsFromStart(ref inputReference, ref sampleReference, sampleCount, _scale);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ConvertBytesFromStart(ref byte input, ref float samples, int sampleCount, float scale)
    {
        int sampleIndex = 0;

        if (Vector.IsHardwareAccelerated && sampleCount >= Vector<byte>.Count)
        {
            int lastBlock = sampleCount - Vector<byte>.Count;

            for (; sampleIndex <= lastBlock; sampleIndex += Vector<byte>.Count)
            {
                ConvertBlock(ref Unsafe.Add(ref input, sampleIndex), ref Unsafe.Add(ref samples, sampleIndex), scale);
            }
        }

        for (; sampleIndex < sampleCount; sampleIndex++)
        {
            Unsafe.Add(ref samples, sampleIndex) = Unsafe.Add(ref input, sampleIndex) * scale;
        }
    }

    /// <summary>
    /// Converts the samples from the last to the first, so that the floats, wider than the samples, never reach a
    /// sample still to be read in the row they share: the samples after the last whole block one by one, then the blocks,
    /// each read whole before its floats are written.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ConvertBytesFromEnd(ref byte input, ref float samples, int sampleCount, float scale)
    {
        int blockCount = 0;

        if (Vector.IsHardwareAccelerated)
        {
            blockCount = sampleCount / Vector<byte>.Count;
        }

        for (int sampleIndex = sampleCount - 1; sampleIndex >= blockCount * Vector<byte>.Count; sampleIndex--)
        {
            Unsafe.Add(ref samples, sampleIndex) = Unsafe.Add(ref input, sampleIndex) * scale;
        }

        for (int block = blockCount - 1; block >= 0; block--)
        {
            int sampleIndex = block * Vector<byte>.Count;
            ConvertBlock(ref Unsafe.Add(ref input, sampleIndex), ref Unsafe.Add(ref samples, sampleIndex), scale);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ConvertShortsFromStart(ref byte input, ref float samples, int sampleCount, float scale)
    {
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            Unsafe.Add(ref samples, sampleIndex) = ReadShort(ref input, sampleIndex) * scale;
        }
    }

    /// <summary>
    /// Converts the samples from the last to the first, so that the floats, wider than the samples, never reach a
    /// sample still to be read in the row they share.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ConvertShortsFromEnd(ref byte input, ref float samples, int sampleCount, float scale)
    {
        for (int sampleIndex = sampleCount - 1; sampleIndex >= 0; sampleIndex--)
        {
            Unsafe.Add(ref samples, sampleIndex) = ReadShort(ref input, sampleIndex) * scale;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadShort(ref byte input, int sampleIndex)
    {
        ushort value = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref input, sampleIndex * sizeof(ushort)));

        if (BitConverter.IsLittleEndian)
        {
            return BinaryPrimitives.ReverseEndianness(value);
        }
        else
        {
            return value;
        }
    }

    /// <summary>
    /// Converts one vector of bytes at <paramref name="input"/> into four vectors of floats at
    /// <paramref name="samples"/>, reading all the bytes before writing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertBlock(ref byte input, ref float samples, float scaleValue)
    {
        Vector<float> scale = new(scaleValue);
        Vector<byte> bytes = Unsafe.ReadUnaligned<Vector<byte>>(ref input);
        Vector.Widen(bytes, out Vector<ushort> lowWords, out Vector<ushort> highWords);
        Vector.Widen(lowWords, out Vector<uint> first, out Vector<uint> second);
        Vector.Widen(highWords, out Vector<uint> third, out Vector<uint> fourth);

        ref byte block = ref Unsafe.As<float, byte>(ref samples);
        int vectorBytes = Vector<float>.Count * sizeof(float);
        Unsafe.WriteUnaligned(ref block, Vector.ConvertToSingle(Vector.AsVectorInt32(first)) * scale);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref block, vectorBytes), Vector.ConvertToSingle(Vector.AsVectorInt32(second)) * scale);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref block, vectorBytes * 2), Vector.ConvertToSingle(Vector.AsVectorInt32(third)) * scale);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref block, vectorBytes * 3), Vector.ConvertToSingle(Vector.AsVectorInt32(fourth)) * scale);
    }
}
