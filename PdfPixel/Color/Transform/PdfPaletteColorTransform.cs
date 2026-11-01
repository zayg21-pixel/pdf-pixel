using PdfPixel.Color.ColorSpace;
using System;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Looks up a palette index and returns the palette color converted by the transform of the palette's
/// base color space. An index outside the palette selects the nearest valid color.
/// </summary>
public sealed class PdfPaletteColorTransform : IPdfColorTransform
{
    private readonly int _highestIndex;
    private readonly float[] _colors;

    /// <summary>
    /// Initializes a transform looking up colors of <paramref name="palette"/>.
    /// </summary>
    /// <param name="palette">Palette the indexes select from.</param>
    /// <param name="baseTransform">Transform of the palette's base color space.</param>
    public PdfPaletteColorTransform(PdfPalette palette, IPdfColorTransform baseTransform)
    {
        if (palette == null)
        {
            throw new ArgumentNullException(nameof(palette));
        }

        if (baseTransform == null)
        {
            throw new ArgumentNullException(nameof(baseTransform));
        }

        if (baseTransform.InputComponentCount != palette.BaseColorSpace.Components)
        {
            throw new ArgumentException("The base transform must read the components of the palette's base color space.", nameof(baseTransform));
        }

        _highestIndex = palette.HighestIndex;
        OutputComponentCount = baseTransform.OutputComponentCount;
        _colors = new float[(_highestIndex + 1) * OutputComponentCount];

        for (int index = 0; index <= _highestIndex; index++)
        {
            ReadOnlySpan<float> color = baseTransform.Transform(palette.GetColor(index).Components.Span);
            color.Slice(0, OutputComponentCount).CopyTo(_colors.AsSpan(index * OutputComponentCount, OutputComponentCount));
        }
    }

    /// <inheritdoc />
    public int InputComponentCount => 1;

    /// <inheritdoc />
    public int OutputComponentCount { get; }

    /// <inheritdoc />
    public ReadOnlySpan<float> Transform(ReadOnlySpan<float> input)
    {
        if (input.IsEmpty)
        {
            throw new ArgumentException("The input holds no index.", nameof(input));
        }

        var index = (int)(input[0] + 0.5f);
        index = Math.Max(0, Math.Min(_highestIndex, index));

        return _colors.AsSpan(index * OutputComponentCount, OutputComponentCount);
    }
}
