using PdfPixel.Fonts.Typeface;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Locates an installed typeface to substitute for a PDF font whose own program is unavailable.
/// Each method is a complete query: the caller states what it needs a glyph for, and the provider
/// owns every step of finding a typeface that supplies it.
/// </summary>
public interface IFontProvider
{
    /// <summary>
    /// Resolves a typeface able to render <paramref name="unicode"/>: the requested family when it
    /// covers the text, then any installed font that does, then the last-resort typeface.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints (stem, weight, width, slant).</param>
    /// <param name="unicode">The Unicode text the resolved typeface must be able to render.</param>
    /// <returns>A typeface to shape <paramref name="unicode"/> against; never <see langword="null"/>.</returns>
    SfntPdfTypeface GetTypefaceByUnicode(in PdfSubstitutionInfo substitutionInfo, string unicode);

    /// <summary>
    /// Resolves the requested family when it addresses its glyphs by character code through a built-in
    /// symbol encoding - as Wingdings and its kin do - and maps <paramref name="characterCode"/> to a
    /// glyph. No Unicode value addresses such a glyph, so this is the only way to reach it.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints (stem, weight, width, slant).</param>
    /// <param name="characterCode">The PDF character code to look up in the family's built-in encoding.</param>
    /// <returns>The resolved typeface, or <see langword="null"/> when the family is not symbol-encoded or does not map the code.</returns>
    SfntPdfTypeface? GetSymbolTypefaceByCode(in PdfSubstitutionInfo substitutionInfo, int characterCode);

    /// <summary>
    /// Returns the last-resort typeface to measure against when a character resolves to no glyph at all.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints, used to match style where possible.</param>
    SfntPdfTypeface GetFallbackTypeface(in PdfSubstitutionInfo substitutionInfo);

    /// <summary>
    /// Disposes and discards every cached typeface that isn't one of the provider's core substitution
    /// targets (e.g. the Standard 14 family candidates and the configured fallback font), freeing
    /// memory held by transient Unicode-fallback resolutions.
    /// </summary>
    void Cleanup();
}
