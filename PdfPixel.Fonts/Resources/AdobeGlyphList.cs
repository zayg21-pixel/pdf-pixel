using PdfPixel.Fonts.Model;
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
    public static Dictionary<PdfFontString, string> GetMap(PdfFontEncoding baseEncoding)
        => (baseEncoding == PdfFontEncoding.ZapfDingbatsEncoding) ? ZapfDingbatsCharacterMap : CharacterMap;
}
