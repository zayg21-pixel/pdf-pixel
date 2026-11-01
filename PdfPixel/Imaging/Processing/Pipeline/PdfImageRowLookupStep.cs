using PdfPixel.Color.Transform;
using PdfPixel.Parsing;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Converts packed single-component samples of 1, 2, 4 or 8 bits to float colors through a table holding
/// the <see cref="PdfColorSampler"/> result of every code. An alpha channel following the component, at the
/// same bit depth, becomes a float in the range 0 to 1.
/// </summary>
public sealed class PdfImageRowLookupStep : IPdfImageRowProcessingStep
{
    private readonly int _width;
    private readonly int _bitsPerComponent;
    private readonly int _outputComponentCount;
    private readonly bool _hasAlpha;
    private readonly int _requiredInputBits;
    private readonly float _alphaScale;
    private readonly float[] _colors;
    private readonly float[] _buffer;

    /// <summary>
    /// Initializes a step converting rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="sampler">Sampler each code's color is taken from, reading one component in the range 0 to 1.</param>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample: 1, 2, 4 or 8.</param>
    /// <param name="outputComponentCount">Number of color components the sampler produces.</param>
    /// <param name="hasAlpha">Whether an alpha channel follows the component of each pixel.</param>
    public PdfImageRowLookupStep(PdfColorSampler sampler, int width, int bitsPerComponent, int outputComponentCount, bool hasAlpha)
    {
        if (sampler == null)
        {
            throw new ArgumentNullException(nameof(sampler));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Bits per component must be 1, 2, 4 or 8.");
        }

        if (outputComponentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputComponentCount), "Output component count must be positive.");
        }

        _width = width;
        _bitsPerComponent = bitsPerComponent;
        _outputComponentCount = outputComponentCount;
        _hasAlpha = hasAlpha;
        _requiredInputBits = width * (hasAlpha ? 2 : 1) * bitsPerComponent;

        int codeCount = 1 << bitsPerComponent;
        float maximumCode = codeCount - 1;
        _alphaScale = 1f / maximumCode;
        _colors = new float[codeCount * outputComponentCount];

        var component = new float[1];

        for (int code = 0; code < codeCount; code++)
        {
            component[0] = code / maximumCode;
            ReadOnlySpan<float> color = sampler.Sample(component);
            color.Slice(0, outputComponentCount).CopyTo(_colors.AsSpan(code * outputComponentCount, outputComponentCount));
        }

        _buffer = new float[width * (outputComponentCount + (hasAlpha ? 1 : 0))];
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
            throw new ArgumentException("The input row holds fewer samples than the step converts.", nameof(input));
        }

        if (_hasAlpha)
        {
            LookupWithAlpha(input);
        }
        else
        {
            LookupOpaque(input);
        }

        return new PdfImageRow(MemoryMarshal.AsBytes(_buffer.AsSpan()), inputRowIndex, hasNextRow: false);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void LookupOpaque(in ReadOnlySpan<byte> input)
    {
        UintBitReaderFixedLength reader = new(input, _bitsPerComponent);
        ref float colorsReference = ref _colors[0];
        ref float destinationReference = ref _buffer[0];
        int destinationIndex = 0;

        for (int x = 0; x < _width; x++)
        {
            int colorStart = (int)reader.Read() * _outputComponentCount;

            for (int component = 0; component < _outputComponentCount; component++)
            {
                Unsafe.Add(ref destinationReference, destinationIndex++) = Unsafe.Add(ref colorsReference, colorStart + component);
            }
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void LookupWithAlpha(in ReadOnlySpan<byte> input)
    {
        UintBitReaderFixedLength reader = new(input, _bitsPerComponent);
        ref float colorsReference = ref _colors[0];
        ref float destinationReference = ref _buffer[0];
        int destinationIndex = 0;

        for (int x = 0; x < _width; x++)
        {
            int colorStart = (int)reader.Read() * _outputComponentCount;

            for (int component = 0; component < _outputComponentCount; component++)
            {
                Unsafe.Add(ref destinationReference, destinationIndex++) = Unsafe.Add(ref colorsReference, colorStart + component);
            }

            Unsafe.Add(ref destinationReference, destinationIndex++) = reader.Read() * _alphaScale;
        }
    }
}
