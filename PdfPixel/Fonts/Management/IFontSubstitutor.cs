using PdfPixel.Fonts.Typeface;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Loads a typeface by substitution parameters from whatever font source is available - the installed
/// system fonts, an explicit in-memory registry, and so on. An implementation only loads: candidate
/// selection, caching, and the lifetime of what it returns all belong to the caller.
/// </summary>
/// <remarks>
/// Every method returns a freshly constructed typeface that the caller owns and disposes. An
/// implementation must never hand back an instance it keeps a reference to, because the caller
/// disposes what it receives.
/// </remarks>
public interface IFontSubstitutor
{
    /// <summary>
    /// Loads the font family named by <paramref name="substitutionInfo"/>, in the closest available
    /// style. Whether the resolved font covers the character being shaped is the caller's decision
    /// to make.
    /// </summary>
    /// <param name="substitutionInfo">Names the font family and style to load.</param>
    /// <returns>A newly constructed typeface, or <see langword="null"/> when the family is not available.</returns>
    SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo);

    /// <summary>
    /// Loads the available font best able to render the first codepoint in <paramref name="unicode"/>,
    /// preferring the family named by <paramref name="substitutionInfo"/>.
    /// </summary>
    /// <param name="substitutionInfo">Names the preferred font family and style to load.</param>
    /// <param name="unicode">The Unicode text whose first codepoint to load a font for.</param>
    /// <returns>A newly constructed typeface, or <see langword="null"/> when no available font can render it.</returns>
    SfntPdfTypeface? ResolveByCharacter(in PdfSubstitutionInfo substitutionInfo, string unicode);

    /// <summary>
    /// Loads the source's own last-resort typeface, used when no family and no character lookup
    /// produced a match.
    /// </summary>
    /// <returns>A newly constructed typeface; never <see langword="null"/>.</returns>
    SfntPdfTypeface GetFallbackTypeface();
}
