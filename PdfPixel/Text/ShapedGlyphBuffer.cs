using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Text;

/// <summary>
/// Growable buffer of shaped glyphs. Reusable across many text runs via <see cref="Clear"/>,
/// which drops the accumulated glyphs without releasing the buffer.
/// </summary>
public sealed class ShapedGlyphBuffer
{
    private const int DefaultCapacity = 16;

    private ShapedGlyph[] _glyphs = [];
    private int _count;

    /// <summary>
    /// Glyphs accumulated since the last <see cref="Clear"/>. Valid until the buffer is next changed.
    /// </summary>
    public ReadOnlySpan<ShapedGlyph> Glyphs => new(_glyphs, 0, _count);

    /// <summary>
    /// Appends <paramref name="glyph"/> to the end of the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in ShapedGlyph glyph)
    {
        EnsureCapacity(_count + 1);

        _glyphs[_count] = glyph;
        _count++;
    }

    /// <summary>
    /// Removes all accumulated glyphs so the buffer can be reused.
    /// </summary>
    public void Clear() => _count = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int requiredCount)
    {
        if (requiredCount <= _glyphs.Length)
        {
            return;
        }

        int newCapacity = (_glyphs.Length == 0) ? DefaultCapacity : _glyphs.Length * 2;
        if (newCapacity < requiredCount)
        {
            newCapacity = requiredCount;
        }

        Array.Resize(ref _glyphs, newCapacity);
    }
}
