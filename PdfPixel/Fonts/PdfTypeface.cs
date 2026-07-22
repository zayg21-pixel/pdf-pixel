using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts;

/// <summary>
/// Wraps a resolved font typeface, either created from raw font-program bytes or from an already-resolved
/// platform typeface. Releases the underlying native typeface via finalizer instead of explicit disposal.
/// </summary>
public sealed class PdfTypeface
{
    private readonly SKTypeface _skTypeface;

    /// <summary>
    /// Creates a typeface from raw font program bytes (TrueType, OpenType, CFF-wrapped, etc.).
    /// </summary>
    /// <param name="fontBytes">The font program bytes.</param>
    public PdfTypeface(in ReadOnlyMemory<byte> fontBytes)
    {
        using SKData data = SKData.CreateCopy(fontBytes.ToArray());
        _skTypeface = SKTypeface.FromData(data) ?? throw new InvalidOperationException("Failed to create typeface from font data.");
    }

    internal PdfTypeface(SKTypeface skTypeface) => _skTypeface = skTypeface;

    internal SKTypeface GetTypeface() => _skTypeface;

    /// <summary>
    /// Creates a font object for text shaping and measurement using this typeface.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SKFont CreateTextFont()
    {
        SKFont font = new()
        {
            Typeface = _skTypeface,
            Size = 1,
            Subpixel = true,
            LinearMetrics = true,
            Hinting = SKFontHinting.Slight
        };

        // Skew/rotation are already represented in the text matrix applied at draw time.
        return font;
    }

    /// <summary>
    /// Returns the glyph IDs for each character in <paramref name="unicode"/>. A value of 0 indicates the
    /// typeface has no glyph for that character.
    /// </summary>
    internal ushort[] GetGlyphs(string unicode)
    {
        using SKFont font = CreateTextFont();
        return font.GetGlyphs(unicode);
    }

    /// <summary>
    /// Returns the shaped glyph widths for each character in <paramref name="unicode"/>.
    /// </summary>
    internal float[] GetGlyphWidths(string unicode)
    {
        using SKFont font = CreateTextFont();
        return font.GetGlyphWidths(unicode);
    }

    /// <summary>
    /// Returns <see langword="true"/> when this typeface contains glyphs for all characters in <paramref name="unicode"/>.
    /// </summary>
    public bool ContainsGlyph(string? unicode)
    {
        if (unicode == null)
        {
            return true;
        }

        ushort[] glyphs = GetGlyphs(unicode);

        for (int i = 0; i < glyphs.Length; i++)
        {
            if (glyphs[i] == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    ~PdfTypeface() => _skTypeface.Dispose();
}
