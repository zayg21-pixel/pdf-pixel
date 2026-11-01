using PdfPixel.Color.Transform;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Converts the color of each float pixel through a <see cref="PdfColorSampler"/>. An alpha channel
/// following the color components passes through unchanged.
/// </summary>
public sealed class PdfImageRowColorStep : IPdfImageRowProcessingStep
{
    private readonly PdfColorSampler _sampler;
    private readonly int _width;
    private readonly int _inputComponentCount;
    private readonly int _outputComponentCount;
    private readonly bool _hasAlpha;
    private readonly float[] _buffer;

    /// <summary>
    /// Initializes a step converting rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="sampler">Sampler each pixel's color runs through.</param>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="inputComponentCount">Number of color components in each input pixel.</param>
    /// <param name="outputComponentCount">Number of color components the sampler produces.</param>
    /// <param name="hasAlpha">Whether an alpha channel follows the color components of each pixel.</param>
    public PdfImageRowColorStep(PdfColorSampler sampler, int width, int inputComponentCount, int outputComponentCount, bool hasAlpha)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (inputComponentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputComponentCount), "Input component count must be positive.");
        }

        if (outputComponentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputComponentCount), "Output component count must be positive.");
        }

        _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        _width = width;
        _inputComponentCount = inputComponentCount;
        _outputComponentCount = outputComponentCount;
        _hasAlpha = hasAlpha;
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
        ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(input);

        if (samples.Length < _width * (_inputComponentCount + (_hasAlpha ? 1 : 0)))
        {
            throw new ArgumentException("The input row holds fewer samples than the step converts.", nameof(input));
        }

        if (_hasAlpha)
        {
            ConvertWithAlpha(samples);
        }
        else
        {
            ConvertOpaque(samples);
        }

        return new PdfImageRow(MemoryMarshal.AsBytes(_buffer.AsSpan()), inputRowIndex, hasNextRow: false);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void ConvertOpaque(in ReadOnlySpan<float> samples)
    {
        for (int x = 0; x < _width; x++)
        {
            ReadOnlySpan<float> color = _sampler.Sample(samples.Slice(x * _inputComponentCount, _inputComponentCount));
            color.Slice(0, _outputComponentCount).CopyTo(_buffer.AsSpan(x * _outputComponentCount, _outputComponentCount));
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void ConvertWithAlpha(in ReadOnlySpan<float> samples)
    {
        int inputStride = _inputComponentCount + 1;
        int outputStride = _outputComponentCount + 1;

        for (int x = 0; x < _width; x++)
        {
            int inputOffset = x * inputStride;
            int outputOffset = x * outputStride;

            ReadOnlySpan<float> color = _sampler.Sample(samples.Slice(inputOffset, _inputComponentCount));
            color.Slice(0, _outputComponentCount).CopyTo(_buffer.AsSpan(outputOffset, _outputComponentCount));
            _buffer[outputOffset + _outputComponentCount] = samples[inputOffset + _inputComponentCount];
        }
    }
}
