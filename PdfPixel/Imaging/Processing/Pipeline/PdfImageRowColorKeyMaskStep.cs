using PdfPixel.Parsing;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Appends an alpha channel to packed samples, at their bit depth: zero where every component lies within
/// its color key range, the maximum code elsewhere.
/// </summary>
public sealed class PdfImageRowColorKeyMaskStep : IPdfImageRowProcessingStep
{
    private readonly int _width;
    private readonly int _componentCount;
    private readonly int _bitsPerComponent;
    private readonly int[] _ranges;
    private readonly uint _maximumCode;
    private readonly int _requiredInputBits;
    private readonly byte[] _buffer;

    /// <summary>
    /// Initializes a step masking rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="componentCount">Number of components in each pixel.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample: 1, 2, 4, 8 or 16.</param>
    /// <param name="ranges">Minimum and maximum code of each component, in component order.</param>
    public PdfImageRowColorKeyMaskStep(int width, int componentCount, int bitsPerComponent, int[] ranges)
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

        if (ranges == null || ranges.Length != componentCount * 2)
        {
            throw new ArgumentException("A minimum and a maximum code are required for every component.", nameof(ranges));
        }

        _width = width;
        _componentCount = componentCount;
        _bitsPerComponent = bitsPerComponent;
        _ranges = ranges;
        _maximumCode = (1u << bitsPerComponent) - 1u;
        _requiredInputBits = width * componentCount * bitsPerComponent;
        _buffer = new byte[((width * (componentCount + 1) * bitsPerComponent) + 7) / 8];
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
            throw new ArgumentException("The input row holds fewer samples than the step masks.", nameof(input));
        }

        UintBitReaderFixedLength reader = new(input, _bitsPerComponent);
        UintBitWriter writer = new(_buffer);

        for (int x = 0; x < _width; x++)
        {
            var isKeyed = true;

            for (int component = 0; component < _componentCount; component++)
            {
                uint code = reader.Read();
                writer.WriteBits(_bitsPerComponent, code);

                if (code < _ranges[component * 2] || code > _ranges[(component * 2) + 1])
                {
                    isKeyed = false;
                }
            }

            writer.WriteBits(_bitsPerComponent, isKeyed ? 0u : _maximumCode);
        }

        writer.Flush();

        return new PdfImageRow(_buffer, inputRowIndex, hasNextRow: false);
    }
}
