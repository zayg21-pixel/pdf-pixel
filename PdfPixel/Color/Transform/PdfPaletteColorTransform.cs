using PdfPixel.Color.ColorSpace;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Looks up a palette index and returns the palette color converted by the transform of the palette's
/// base color space. An index outside the palette selects the nearest valid color.
/// </summary>
public sealed class PdfPaletteColorTransform : IPdfColorTransform
{
    private readonly int _highestIndex;
    private readonly float[] _colors;
    private readonly uint _colorByteCount;

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
        _colorByteCount = (uint)(OutputComponentCount * sizeof(float));

        var baseColor = new float[Math.Max(1, baseTransform.InputComponentCount)];

        for (int index = 0; index <= _highestIndex; index++)
        {
            ReadOnlySpan<float> components = palette.GetColor(index).Components.Span;

            Array.Clear(baseColor, 0, baseColor.Length);
            components.Slice(0, Math.Min(components.Length, baseColor.Length)).CopyTo(baseColor);
            baseTransform.Transform(ref baseColor[0], ref _colors[index * OutputComponentCount]);
        }
    }

    /// <inheritdoc />
    public int InputComponentCount => 1;

    /// <inheritdoc />
    public int OutputComponentCount { get; }

    /// <inheritdoc />
    public void Transform(ref float input, ref float output)
    {
        var index = (int)(input + 0.5f);
        index = Math.Max(0, Math.Min(_highestIndex, index));

        ref float color = ref Unsafe.Add(ref MemoryMarshal.GetReference(_colors.AsSpan()), index * OutputComponentCount);
        Unsafe.CopyBlockUnaligned(ref Unsafe.As<float, byte>(ref output), ref Unsafe.As<float, byte>(ref color), _colorByteCount);
    }
}
