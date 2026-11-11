using PdfPixel.Parsing;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    private readonly int _outputLength;
    private readonly uint[] _codes;

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
        _outputLength = ((width * (componentCount + 1) * bitsPerComponent) + 7) / 8;
        _codes = new uint[componentCount];
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => _outputLength;

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
            throw new ArgumentException("The input row holds fewer samples than the step masks.", nameof(input));
        }

        if (Unsafe.AreSame(ref MemoryMarshal.GetReference(input), ref MemoryMarshal.GetReference(output)))
        {
            MaskFromEnd(ref MemoryMarshal.GetReference(output));
        }
        else
        {
            MaskFromStart(input, output);
        }
    }

    /// <summary>
    /// Masks the pixels from the first to the last, reading <paramref name="input"/> and writing
    /// <paramref name="output"/>.
    /// </summary>
    private void MaskFromStart(in ReadOnlySpan<byte> input, in Span<byte> output)
    {
        UintBitReaderFixedLength reader = new(input, _bitsPerComponent);
        UintBitWriter writer = new(output);

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
    }

    /// <summary>
    /// Masks the pixels of the row at <paramref name="row"/> from the last to the first, each pixel's codes read
    /// before it is written, so that the pixels, one sample wider each, never reach a code still to be read.
    /// </summary>
    private void MaskFromEnd(ref byte row)
    {
        int outputStride = _componentCount + 1;

        for (int x = _width - 1; x >= 0; x--)
        {
            int inputIndex = x * _componentCount;
            var isKeyed = true;

            for (int component = 0; component < _componentCount; component++)
            {
                uint code = PdfPackedSamples.Read(ref row, inputIndex + component, _bitsPerComponent);
                _codes[component] = code;

                if (code < _ranges[component * 2] || code > _ranges[(component * 2) + 1])
                {
                    isKeyed = false;
                }
            }

            int outputIndex = x * outputStride;

            for (int component = 0; component < _componentCount; component++)
            {
                PdfPackedSamples.Write(ref row, outputIndex + component, _bitsPerComponent, _codes[component]);
            }

            PdfPackedSamples.Write(ref row, outputIndex + _componentCount, _bitsPerComponent, isKeyed ? 0u : _maximumCode);
        }
    }
}
