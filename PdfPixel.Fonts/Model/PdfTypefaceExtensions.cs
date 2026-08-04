using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Glyph-containment helpers for <see cref="IPdfTypeface"/>.
/// </summary>
public static class PdfTypefaceExtensions
{
    /// <summary>
    /// Determines whether <paramref name="typeface"/> maps every character in <paramref name="unicode"/> to a glyph.
    /// </summary>
    /// <param name="typeface">The typeface to check.</param>
    /// <param name="unicode">The unicode text to check, or <see langword="null"/> to skip the check entirely.</param>
    public static bool ContainsAllGlyphs(this IPdfTypeface typeface, string? unicode)
    {
        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        if (unicode == null || unicode.Length == 0)
        {
            return true;
        }

        int index = 0;
        while (index < unicode.Length)
        {
            int codepointLength = (char.IsSurrogatePair(unicode, index)) ? 2 : 1;
            string character = unicode.Substring(index, codepointLength);
            if (typeface.GetGid(character) == null)
            {
                return false;
            }

            index += codepointLength;
        }

        return true;
    }

    /// <summary>
    /// Returns the glyph ID for each Unicode codepoint in <paramref name="unicode"/>, in order.
    /// A <see langword="null"/> entry indicates the typeface has no glyph for that codepoint.
    /// </summary>
    /// <param name="typeface">The typeface to look up glyphs in.</param>
    /// <param name="unicode">The unicode text to map to glyph IDs.</param>
    public static ushort?[] GetGlyphs(this IPdfTypeface typeface, string unicode)
    {
        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        if (unicode == null)
        {
            throw new ArgumentNullException(nameof(unicode));
        }

        List<ushort?> gids = [];
        int index = 0;
        while (index < unicode.Length)
        {
            int codepointLength = (char.IsSurrogatePair(unicode, index)) ? 2 : 1;
            string character = unicode.Substring(index, codepointLength);
            gids.Add(typeface.GetGid(character));
            index += codepointLength;
        }

        return [.. gids];
    }

    /// <summary>
    /// Returns the total advance width, in em-relative units, across every Unicode codepoint in <paramref name="unicode"/>.
    /// </summary>
    /// <param name="typeface">The typeface to look up the width in.</param>
    /// <param name="unicode">The unicode text to measure.</param>
    public static float GetWidth(this IPdfTypeface typeface, string unicode)
    {
        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        if (unicode == null)
        {
            throw new ArgumentNullException(nameof(unicode));
        }

        float total = 0f;
        int index = 0;
        while (index < unicode.Length)
        {
            int codepointLength = (char.IsSurrogatePair(unicode, index)) ? 2 : 1;
            string character = unicode.Substring(index, codepointLength);
            ushort gid = typeface.GetGid(character) ?? 0;
            total += typeface.GetWidth(gid) ?? 0f;
            index += codepointLength;
        }

        return total;
    }

    /// <summary>
    /// Returns the advance width, in em-relative units, for each glyph ID in <paramref name="gids"/>, in order.
    /// A <see langword="null"/> entry contributes a width of 0.
    /// </summary>
    /// <param name="typeface">The typeface to look up widths in.</param>
    /// <param name="gids">The glyph IDs to measure.</param>
    public static float[] GetWidths(this IPdfTypeface typeface, ushort?[] gids)
    {
        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        if (gids == null)
        {
            throw new ArgumentNullException(nameof(gids));
        }

        var widths = new float[gids.Length];
        for (int i = 0; i < gids.Length; i++)
        {
            ushort? gid = gids[i];
            widths[i] = (gid.HasValue) ? typeface.GetWidth(gid.Value) ?? 0f : 0f;
        }

        return widths;
    }
}
