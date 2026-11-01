using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Expands 8-bit gray with alpha, or 8-bit RGB, to 8-bit RGBA. RGB pixels become opaque.
/// </summary>
public sealed class PdfImageRowExpandToRgbaStep : IPdfImageRowProcessingStep
{
    private const int GrayAlphaComponentCount = 2;
    private const int RgbComponentCount = 3;
    private const int RgbaComponentCount = 4;

    private readonly int _width;
    private readonly int _componentCount;
    private readonly byte[] _buffer;

    /// <summary>
    /// Initializes a step expanding rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="componentCount">Number of components in each input pixel: 2 for gray with alpha, 3 for RGB.</param>
    public PdfImageRowExpandToRgbaStep(int width, int componentCount)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (componentCount != GrayAlphaComponentCount && componentCount != RgbComponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be 2 for gray with alpha or 3 for RGB.");
        }

        _width = width;
        _componentCount = componentCount;
        _buffer = new byte[width * RgbaComponentCount];
    }

    /// <inheritdoc />
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfImageRow Process(ReadOnlySpan<byte> input, int inputRowIndex)
    {
        if (input.Length < _width * _componentCount)
        {
            throw new ArgumentException("The input row holds fewer samples than the step reads.", nameof(input));
        }

        if (_componentCount == GrayAlphaComponentCount)
        {
            ExpandInterleavedGrayToRgba(input, _buffer, _width);
        }
        else
        {
            ExpandRgbToOpaqueRgba(input, _buffer, _width);
        }

        return new PdfImageRow(_buffer, inputRowIndex, hasNextRow: false);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ExpandInterleavedGrayToRgba(in ReadOnlySpan<byte> normalizedRow, in Span<byte> destRow, int pixelCount)
    {
        ref byte source = ref MemoryMarshal.GetReference(normalizedRow);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(destRow));

        for (int x = 0; x < pixelCount; x++)
        {
            int offset = x * 2;
            uint gray = Unsafe.Add(ref source, offset);
            uint alpha = Unsafe.Add(ref source, offset + 1);
            Unsafe.Add(ref destPixel, x) = gray | (gray << 8) | (gray << 16) | (alpha << 24);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ExpandRgbToOpaqueRgba(in ReadOnlySpan<byte> normalizedRow, in Span<byte> destRow, int pixelCount)
    {
        ref byte source = ref MemoryMarshal.GetReference(normalizedRow);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(destRow));
        int lastPixel = pixelCount - 1;

        for (int x = 0; x < lastPixel; x++)
        {
            uint rgb = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, x * 3));
            Unsafe.Add(ref destPixel, x) = rgb | 0xFF000000;
        }

        int lastOffset = lastPixel * 3;
        uint red = Unsafe.Add(ref source, lastOffset);
        uint green = Unsafe.Add(ref source, lastOffset + 1);
        uint blue = Unsafe.Add(ref source, lastOffset + 2);
        Unsafe.Add(ref destPixel, lastPixel) = red | (green << 8) | (blue << 16) | 0xFF000000;
    }
}
