using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Inverts packed samples of any bit depth, replacing each code c with the maximum code minus c.
/// </summary>
public sealed class PdfImageRowInvertStep : IPdfImageRowProcessingStep
{
    private readonly byte[] _buffer;

    /// <summary>
    /// Initializes a step inverting rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="componentCount">Number of components in each pixel.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample.</param>
    public PdfImageRowInvertStep(int width, int componentCount, int bitsPerComponent)
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

        _buffer = new byte[((width * componentCount * bitsPerComponent) + 7) / 8];
    }

    /// <inheritdoc />
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfImageRow Process(ReadOnlySpan<byte> input, int inputRowIndex)
    {
        if (input.Length < _buffer.Length)
        {
            throw new ArgumentException("The input row holds fewer samples than the step inverts.", nameof(input));
        }

        InvertBytes(input, _buffer);

        return new PdfImageRow(_buffer, inputRowIndex, hasNextRow: false);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void InvertBytes(in ReadOnlySpan<byte> source, in Span<byte> destination)
    {
        int byteCount = destination.Length;
        ref byte sourceByte = ref MemoryMarshal.GetReference(source);
        ref byte destinationByte = ref MemoryMarshal.GetReference(destination);
        int blockCount = byteCount >> 3;

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            int offset = blockIndex << 3;
            ulong block = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref sourceByte, offset));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationByte, offset), ~block);
        }

        for (int index = blockCount << 3; index < byteCount; index++)
        {
            Unsafe.Add(ref destinationByte, index) = (byte)~Unsafe.Add(ref sourceByte, index);
        }
    }
}
