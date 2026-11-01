using System;
using System.Numerics;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Converts the components of a color through a <see cref="ChainedColorTransform"/> of up to four components.
/// </summary>
public sealed class PdfChainedColorTransform : IPdfColorTransform
{
    private const int MaximumComponentCount = 4;

    private readonly ChainedColorTransform _colorTransform;
    private readonly float[] _buffer;

    /// <summary>
    /// Initializes a transform converting through <paramref name="colorTransform"/>.
    /// </summary>
    /// <param name="colorTransform">Chain the components are converted through.</param>
    /// <param name="inputComponentCount">Number of components the transform reads, 0 to 4.</param>
    /// <param name="outputComponentCount">Number of components the transform produces, 1 to 4.</param>
    public PdfChainedColorTransform(ChainedColorTransform colorTransform, int inputComponentCount, int outputComponentCount)
    {
        if (inputComponentCount < 0 || inputComponentCount > MaximumComponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(inputComponentCount), "Input component count must be 0 to 4.");
        }

        if (outputComponentCount < 1 || outputComponentCount > MaximumComponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(outputComponentCount), "Output component count must be 1 to 4.");
        }

        _colorTransform = colorTransform ?? throw new ArgumentNullException(nameof(colorTransform));
        InputComponentCount = inputComponentCount;
        OutputComponentCount = outputComponentCount;
        _buffer = new float[MaximumComponentCount];
    }

    /// <inheritdoc />
    public int InputComponentCount { get; }

    /// <inheritdoc />
    public int OutputComponentCount { get; }

    /// <inheritdoc />
    public ReadOnlySpan<float> Transform(ReadOnlySpan<float> input)
    {
        Vector4 color = _colorTransform.Transform(ColorVectorUtilities.ToVector4WithOnePadding(input.Slice(0, Math.Min(input.Length, InputComponentCount))));

        _buffer[0] = color.X;
        _buffer[1] = color.Y;
        _buffer[2] = color.Z;
        _buffer[3] = color.W;

        return _buffer.AsSpan(0, OutputComponentCount);
    }
}
