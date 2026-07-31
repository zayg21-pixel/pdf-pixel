using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Resources;

/// <summary>
/// Represents Adobe Glyph list as map of characters.
/// </summary>
public static class AdobeGlyphList
{
    static AdobeGlyphList()
    {
        byte[] aglData = FontResourceLoader.GetResource("glyphlist.bin");
        CharacterMap = new Dictionary<PdfFontString, string>();
        FontResourceConverter.ReadFromCharacterMapBlob(aglData, CharacterMap);

        // AGL Overrides contains overrides for AGL Symbols from private user area
        byte[] aglOverridesData = FontResourceLoader.GetResource("AglOverrides.bin");
        FontResourceConverter.ReadFromCharacterMapBlob(aglOverridesData, CharacterMap);

        // Zapf Dingbats glyph names (e.g. "a1", "a224") collide with the arbitrary
        // "aNNN"-style names some PDF producers assign to unrelated glyphs in other fonts.
        // Kept in a separate map so only actual Zapf Dingbats fonts consult it.
        byte[] aglZapfDingbatsData = FontResourceLoader.GetResource("zapfdingbats.bin");
        ZapfDingbatsCharacterMap = new Dictionary<PdfFontString, string>();
        FontResourceConverter.ReadFromCharacterMapBlob(aglZapfDingbatsData, ZapfDingbatsCharacterMap);
    }

    /// <summary>
    /// AGL with overrides for PUA symbols. Does not include Zapf Dingbats names.
    /// </summary>
    public static Dictionary<PdfFontString, string> CharacterMap { get; }

    /// <summary>
    /// Zapf Dingbats glyph name to Unicode symbol mapping. Only applies to Zapf Dingbats fonts,
    /// since its glyph names (e.g. "a1") collide with unrelated arbitrary glyph names in other fonts.
    /// </summary>
    public static Dictionary<PdfFontString, string> ZapfDingbatsCharacterMap { get; }

    /// <summary>
    /// Returns the glyph name to Unicode map appropriate for the specified base encoding.
    /// </summary>
    private static Dictionary<PdfFontString, string> GetMap(PdfFontEncoding baseEncoding)
        => (baseEncoding == PdfFontEncoding.ZapfDingbatsEncoding) ? ZapfDingbatsCharacterMap : CharacterMap;

    /// <summary>
    /// Resolves a glyph name to its Unicode string, first via the table for <paramref name="baseEncoding"/>,
    /// then via the Adobe Glyph List's algorithmic naming convention.
    /// </summary>
    /// <param name="baseEncoding">The base encoding whose table to consult first.</param>
    /// <param name="name">The glyph name to resolve.</param>
    /// <param name="unicode">The resolved Unicode string, or <see langword="null"/> if unresolved.</param>
    /// <returns><see langword="true"/> if the glyph name resolved to a Unicode string.</returns>
    public static bool TryGetUnicode(PdfFontEncoding baseEncoding, in PdfFontString name, out string? unicode)
    {
        if (GetMap(baseEncoding).TryGetValue(name, out unicode))
        {
            return true;
        }

        unicode = ParseUniGlyphName(name);
        return unicode != null;
    }

    private static string? ParseUniGlyphName(in PdfFontString name)
    {
        ReadOnlySpan<byte> bytes = name.Value.Span;

        ReadOnlySpan<byte> hexDigits;
        if (bytes.Length == 7 && bytes.Slice(0, 3).SequenceEqual("uni"u8))
        {
            hexDigits = bytes.Slice(3);
        }
        else if (bytes.Length is >= 5 and <= 7 && bytes[0] == (byte)'u')
        {
            hexDigits = bytes.Slice(1);
        }
        else
        {
            return null;
        }

        int codepoint = 0;
        foreach (byte digit in hexDigits)
        {
            int digitValue;
            if (digit is >= (byte)'0' and <= (byte)'9')
            {
                digitValue = digit - (byte)'0';
            }
            else if (digit is >= (byte)'A' and <= (byte)'F')
            {
                digitValue = (digit - (byte)'A') + 10;
            }
            else
            {
                // Lower-case letters are rejected here so names like "uacute" fall through
                // instead of being misread as a hex code point.
                return null;
            }

            codepoint = (codepoint << 4) | digitValue;
        }

        return char.ConvertFromUtf32(codepoint);
    }
}
