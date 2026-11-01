using PdfPixel.Parsing;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Converts packed samples of any bit depth to float samples in the range 0 to 1, without resampling.
/// </summary>
public sealed class PdfImageRowUnpackStep : IPdfImageRowProcessingStep
{
    private const int BitsPerByte = 8;
    private const int ByteValueCount = 256;

    private readonly int _bitsPerComponent;
    private readonly int _requiredInputBits;
    private readonly float _scale;
    private readonly float[] _buffer;

    // For 1 bit per component: the eight samples of every byte value, in reading order.
    private readonly float[]? _bitSamples;

    /// <summary>
    /// Initializes a step unpacking rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="componentCount">Number of components in each pixel.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample.</param>
    public PdfImageRowUnpackStep(int width, int componentCount, int bitsPerComponent)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (componentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be positive.");
        }

        if (bitsPerComponent < 1 || bitsPerComponent > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Bits per component must be 1 to 16.");
        }

        _bitsPerComponent = bitsPerComponent;
        _buffer = new float[width * componentCount];
        _requiredInputBits = _buffer.Length * bitsPerComponent;

        uint maxCode = (1u << bitsPerComponent) - 1u;
        _scale = 1f / maxCode;

        if (bitsPerComponent == 1)
        {
            _bitSamples = BuildBitSamples();
        }
    }

    /// <inheritdoc />
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfImageRow Process(ReadOnlySpan<byte> input, int inputRowIndex)
    {
        if ((long)input.Length * 8 < _requiredInputBits)
        {
            throw new ArgumentException("The input row holds fewer samples than the step unpacks.", nameof(input));
        }

        if (_bitSamples != null)
        {
            UnpackSingleBits(input, _bitSamples);
        }
        else if (_bitsPerComponent == 8)
        {
            UnpackBytes(input);
        }
        else
        {
            UnpackBits(input);
        }

        return new PdfImageRow(MemoryMarshal.AsBytes(_buffer.AsSpan()), inputRowIndex, hasNextRow: false);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpackSingleBits(in ReadOnlySpan<byte> input, float[] bitSamples)
    {
        ref byte sourceReference = ref MemoryMarshal.GetReference(input);
        ref EightSampleBlock samplesBlock = ref Unsafe.As<float, EightSampleBlock>(ref bitSamples[0]);
        ref EightSampleBlock destinationBlock = ref Unsafe.As<float, EightSampleBlock>(ref _buffer[0]);
        int fullByteCount = _buffer.Length / BitsPerByte;

        for (int byteIndex = 0; byteIndex < fullByteCount; byteIndex++)
        {
            Unsafe.Add(ref destinationBlock, byteIndex) = Unsafe.Add(ref samplesBlock, Unsafe.Add(ref sourceReference, byteIndex));
        }

        int remainingSamples = _buffer.Length - (fullByteCount * BitsPerByte);

        if (remainingSamples > 0)
        {
            ref byte samples = ref Unsafe.As<EightSampleBlock, byte>(ref Unsafe.Add(ref samplesBlock, Unsafe.Add(ref sourceReference, fullByteCount)));
            ref byte destination = ref Unsafe.As<EightSampleBlock, byte>(ref Unsafe.Add(ref destinationBlock, fullByteCount));
            Unsafe.CopyBlockUnaligned(ref destination, ref samples, (uint)(remainingSamples * sizeof(float)));
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpackBytes(in ReadOnlySpan<byte> input)
    {
        ref byte sourceReference = ref MemoryMarshal.GetReference(input);
        ref float destinationReference = ref _buffer[0];
        int sampleIndex = 0;

        if (Vector.IsHardwareAccelerated && _buffer.Length >= Vector<byte>.Count)
        {
            Vector<float> scale = new(_scale);
            int floatCount = Vector<float>.Count;
            int lastBlock = _buffer.Length - Vector<byte>.Count;

            for (; sampleIndex <= lastBlock; sampleIndex += Vector<byte>.Count)
            {
                Vector<byte> bytes = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.Add(ref sourceReference, sampleIndex));
                Vector.Widen(bytes, out Vector<ushort> lowWords, out Vector<ushort> highWords);
                Vector.Widen(lowWords, out Vector<uint> first, out Vector<uint> second);
                Vector.Widen(highWords, out Vector<uint> third, out Vector<uint> fourth);

                ref byte destination = ref Unsafe.As<float, byte>(ref Unsafe.Add(ref destinationReference, sampleIndex));
                int vectorBytes = floatCount * sizeof(float);
                Unsafe.WriteUnaligned(ref destination, Vector.ConvertToSingle(Vector.AsVectorInt32(first)) * scale);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, vectorBytes), Vector.ConvertToSingle(Vector.AsVectorInt32(second)) * scale);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, vectorBytes * 2), Vector.ConvertToSingle(Vector.AsVectorInt32(third)) * scale);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, vectorBytes * 3), Vector.ConvertToSingle(Vector.AsVectorInt32(fourth)) * scale);
            }
        }

        for (; sampleIndex < _buffer.Length; sampleIndex++)
        {
            Unsafe.Add(ref destinationReference, sampleIndex) = Unsafe.Add(ref sourceReference, sampleIndex) * _scale;
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpackBits(in ReadOnlySpan<byte> input)
    {
        UintBitReaderFixedLength reader = new(input, _bitsPerComponent);
        ref float destinationReference = ref _buffer[0];

        for (int sampleIndex = 0; sampleIndex < _buffer.Length; sampleIndex++)
        {
            Unsafe.Add(ref destinationReference, sampleIndex) = reader.Read() * _scale;
        }
    }

    private static float[] BuildBitSamples()
    {
        var bitSamples = new float[ByteValueCount * BitsPerByte];

        for (int byteValue = 0; byteValue < ByteValueCount; byteValue++)
        {
            for (int bitIndex = 0; bitIndex < BitsPerByte; bitIndex++)
            {
                bitSamples[(byteValue * BitsPerByte) + bitIndex] = (byteValue >> (BitsPerByte - 1 - bitIndex)) & 1;
            }
        }

        return bitSamples;
    }

    // The samples of one byte at 1 bit per component: eight floats.
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    private struct EightSampleBlock
    {
    }
}
