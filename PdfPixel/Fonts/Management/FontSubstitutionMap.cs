using PdfPixel.Fonts.Mapping;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Names the font families that stand in for each Standard 14 font, together with the family to fall
/// back to when nothing else matches.
/// </summary>
public sealed class FontSubstitutionMap
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontSubstitutionMap"/> class.
    /// </summary>
    /// <param name="candidates">Family names to try for each Standard 14 font, in preference order.</param>
    /// <param name="fallbackFamilyName">
    /// Family name to resolve when no candidate and no character lookup matched, or <see langword="null"/>
    /// to go straight to <see cref="IFontSubstitutor.GetFallbackTypeface"/>.
    /// </param>
    public FontSubstitutionMap(IReadOnlyDictionary<PdfStandardFontName, IReadOnlyList<string>> candidates, string? fallbackFamilyName)
    {
        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        Candidates = candidates;
        FallbackFamilyName = fallbackFamilyName;
    }

    /// <summary>
    /// Family names to try for each Standard 14 font, in preference order.
    /// </summary>
    public IReadOnlyDictionary<PdfStandardFontName, IReadOnlyList<string>> Candidates { get; }

    /// <summary>
    /// Family name to resolve when no candidate and no character lookup matched, or <see langword="null"/>
    /// when the font source's own last-resort typeface should be used directly.
    /// </summary>
    public string? FallbackFamilyName { get; }

    /// <summary>
    /// Returns a copy of this map that falls back to <paramref name="fallbackFamilyName"/> instead.
    /// </summary>
    /// <param name="fallbackFamilyName">The family name to fall back to.</param>
    public FontSubstitutionMap WithFallbackFamily(string fallbackFamilyName) => new(Candidates, fallbackFamilyName);
}
