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
    private readonly int _sampleCount;
    private readonly int _requiredInputBits;
    private readonly float _scale;

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
        _sampleCount = width * componentCount;
        _requiredInputBits = _sampleCount * bitsPerComponent;

        uint maxCode = (1u << bitsPerComponent) - 1u;
        _scale = 1f / maxCode;

        if (bitsPerComponent == 1)
        {
            _bitSamples = BuildBitSamples();
        }
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => _sampleCount * sizeof(float);

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
        if ((long)input.Length * 8 < _requiredInputBits)
        {
            throw new ArgumentException("The input row holds fewer samples than the step unpacks.", nameof(input));
        }

        Span<float> destination = MemoryMarshal.Cast<byte, float>(output);
        bool sharesBuffer = Unsafe.AreSame(ref MemoryMarshal.GetReference(input), ref MemoryMarshal.GetReference(output));

        if (_bitSamples != null)
        {
            UnpackSingleBits(input, _bitSamples, destination, sharesBuffer);
        }
        else if (sharesBuffer)
        {
            UnpackFromEnd(input, destination);
        }
        else if (_bitsPerComponent == 8)
        {
            UnpackBytes(input, destination);
        }
        else
        {
            UnpackBits(input, destination);
        }
    }

    /// <summary>
    /// Unpacks the samples from the last to the first, so that the floats, wider than the samples, never reach a
    /// sample still to be read in the row they share.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpackFromEnd(in ReadOnlySpan<byte> input, in Span<float> destination)
    {
        ref byte sourceReference = ref MemoryMarshal.GetReference(input);
        ref float destinationReference = ref MemoryMarshal.GetReference(destination);

        for (int sampleIndex = _sampleCount - 1; sampleIndex >= 0; sampleIndex--)
        {
            Unsafe.Add(ref destinationReference, sampleIndex) = PdfPackedSamples.Read(ref sourceReference, sampleIndex, _bitsPerComponent) * _scale;
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpackSingleBits(in ReadOnlySpan<byte> input, float[] bitSamples, in Span<float> destination, bool fromEnd)
    {
        ref byte sourceReference = ref MemoryMarshal.GetReference(input);
        ref EightSampleBlock samplesBlock = ref Unsafe.As<float, EightSampleBlock>(ref bitSamples[0]);
        ref EightSampleBlock destinationBlock = ref Unsafe.As<float, EightSampleBlock>(ref MemoryMarshal.GetReference(destination));
        int fullByteCount = _sampleCount / BitsPerByte;
        int remainingSamples = _sampleCount - (fullByteCount * BitsPerByte);

        // From the end, every byte's eight floats land past the bytes still to be read, the last partial byte first.
        if (remainingSamples > 0)
        {
            ref byte samples = ref Unsafe.As<EightSampleBlock, byte>(ref Unsafe.Add(ref samplesBlock, Unsafe.Add(ref sourceReference, fullByteCount)));
            ref byte remainder = ref Unsafe.As<EightSampleBlock, byte>(ref Unsafe.Add(ref destinationBlock, fullByteCount));
            Unsafe.CopyBlockUnaligned(ref remainder, ref samples, (uint)(remainingSamples * sizeof(float)));
        }

        if (fromEnd)
        {
            for (int byteIndex = fullByteCount - 1; byteIndex >= 0; byteIndex--)
            {
                Unsafe.Add(ref destinationBlock, byteIndex) = Unsafe.Add(ref samplesBlock, Unsafe.Add(ref sourceReference, byteIndex));
            }
        }
        else
        {
            for (int byteIndex = 0; byteIndex < fullByteCount; byteIndex++)
            {
                Unsafe.Add(ref destinationBlock, byteIndex) = Unsafe.Add(ref samplesBlock, Unsafe.Add(ref sourceReference, byteIndex));
            }
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpackBytes(in ReadOnlySpan<byte> input, in Span<float> destination)
    {
        ref byte sourceReference = ref MemoryMarshal.GetReference(input);
        ref float destinationReference = ref MemoryMarshal.GetReference(destination);
        int sampleIndex = 0;

        if (Vector.IsHardwareAccelerated && _sampleCount >= Vector<byte>.Count)
        {
            Vector<float> scale = new(_scale);
            int floatCount = Vector<float>.Count;
            int lastBlock = _sampleCount - Vector<byte>.Count;

            for (; sampleIndex <= lastBlock; sampleIndex += Vector<byte>.Count)
            {
                Vector<byte> bytes = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.Add(ref sourceReference, sampleIndex));
                Vector.Widen(bytes, out Vector<ushort> lowWords, out Vector<ushort> highWords);
                Vector.Widen(lowWords, out Vector<uint> first, out Vector<uint> second);
                Vector.Widen(highWords, out Vector<uint> third, out Vector<uint> fourth);

                ref byte block = ref Unsafe.As<float, byte>(ref Unsafe.Add(ref destinationReference, sampleIndex));
                int vectorBytes = floatCount * sizeof(float);
                Unsafe.WriteUnaligned(ref block, Vector.ConvertToSingle(Vector.AsVectorInt32(first)) * scale);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref block, vectorBytes), Vector.ConvertToSingle(Vector.AsVectorInt32(second)) * scale);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref block, vectorBytes * 2), Vector.ConvertToSingle(Vector.AsVectorInt32(third)) * scale);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref block, vectorBytes * 3), Vector.ConvertToSingle(Vector.AsVectorInt32(fourth)) * scale);
            }
        }

        for (; sampleIndex < _sampleCount; sampleIndex++)
        {
            Unsafe.Add(ref destinationReference, sampleIndex) = Unsafe.Add(ref sourceReference, sampleIndex) * _scale;
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void UnpackBits(in ReadOnlySpan<byte> input, in Span<float> destination)
    {
        UintBitReaderFixedLength reader = new(input, _bitsPerComponent);
        ref float destinationReference = ref MemoryMarshal.GetReference(destination);

        for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
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
