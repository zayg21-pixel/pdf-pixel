using PdfPixel.Models;
using PdfPixel.Resources;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Provides the built-in CID-to-Unicode glyph maps for well-known non-embedded standard TrueType
/// fonts. Non-embedded CIDFontType2 fonts with an Identity CID ordering are commonly produced with
/// CIDs equal to the glyph indices of one of these fonts, so this table lets such CIDs be resolved
/// to Unicode without relying on the font's own (often missing or unusable) /ToUnicode CMap.
/// </summary>
public static class StandardFontGlyphMapProvider
{
    private static readonly Lazy<Dictionary<uint, string>> _base = new(() => Load("StandardFontGlyphMap.bin"));
    private static readonly Lazy<Dictionary<uint, string>> _arialBlack = new(() => Load("StandardFontGlyphMapArialBlack.bin"));
    private static readonly Lazy<Dictionary<uint, string>> _calibri = new(() => Load("StandardFontGlyphMapCalibri.bin"));

    /// <summary>
    /// Resolves a CID to its Unicode string, applying the supplemental overrides for
    /// <paramref name="baseFontName"/> when it names a font with known overrides (e.g. "ArialBlack",
    /// "Arial-Black", "Calibri"). <paramref name="baseFontName"/> is the font's raw /BaseFont name,
    /// not a substitution-normalized family stem, since "Black" is otherwise stripped as a weight hint.
    /// </summary>
    /// <param name="baseFontName">The font's raw /BaseFont name.</param>
    /// <param name="cid">The CID to resolve.</param>
    /// <param name="unicode">The resolved Unicode string, or <see langword="null"/> if unresolved.</param>
    /// <returns><see langword="true"/> if the CID resolved to a Unicode string.</returns>
    public static bool TryGetUnicode(in PdfString baseFontName, uint cid, out string? unicode)
    {
        string name = baseFontName.ToString();

        if (IsArialBlack(name) && _arialBlack.Value.TryGetValue(cid, out unicode))
        {
            return true;
        }

        if (name.IndexOf("Calibri", StringComparison.OrdinalIgnoreCase) >= 0
            && _calibri.Value.TryGetValue(cid, out unicode))
        {
            return true;
        }

        return _base.Value.TryGetValue(cid, out unicode);
    }

    private static bool IsArialBlack(string baseFontName)
    {
        return baseFontName.IndexOf("ArialBlack", StringComparison.OrdinalIgnoreCase) >= 0
            || baseFontName.IndexOf("Arial-Black", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Dictionary<uint, string> Load(string resourceName)
    {
        byte[] resource = PdfResourceLoader.GetResource($"External.{resourceName}");
        Dictionary<uint, string> dict = [];
        PdfTextResourceConverter.ReadFromCidToUnicodeMapBlob(resource, dict);
        return dict;
    }
}
