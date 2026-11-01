using PdfPixel.Functions;
using System;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Maps tints through a tint transform function and converts the result by the transform of the
/// alternate color space. Without a function, the tints reach the alternate transform unchanged.
/// </summary>
public sealed class PdfTintColorTransform : IPdfColorTransform
{
    private readonly PdfFunction? _tintTransform;
    private readonly IPdfColorTransform _alternateTransform;

    /// <summary>
    /// Initializes a transform mapping <paramref name="componentCount"/> tints.
    /// </summary>
    /// <param name="componentCount">Number of tints of each color.</param>
    /// <param name="tintTransform">Function mapping the tints to the alternate color space, or null when absent.</param>
    /// <param name="alternateTransform">Transform of the alternate color space.</param>
    public PdfTintColorTransform(int componentCount, PdfFunction? tintTransform, IPdfColorTransform alternateTransform)
    {
        if (componentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be positive.");
        }

        InputComponentCount = componentCount;
        _tintTransform = tintTransform;
        _alternateTransform = alternateTransform ?? throw new ArgumentNullException(nameof(alternateTransform));
    }

    /// <inheritdoc />
    public int InputComponentCount { get; }

    /// <inheritdoc />
    public int OutputComponentCount => _alternateTransform.OutputComponentCount;

    /// <inheritdoc />
    public ReadOnlySpan<float> Transform(ReadOnlySpan<float> input)
    {
        if (_tintTransform == null)
        {
            return _alternateTransform.Transform(input);
        }
        else
        {
            return _alternateTransform.Transform(_tintTransform.Evaluate(input.Slice(0, Math.Min(input.Length, InputComponentCount))));
        }
    }
}
