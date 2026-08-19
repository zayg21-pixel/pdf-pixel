using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Locates a typeface to substitute for a PDF font whose own program is unavailable.
/// </summary>
public sealed class FontProvider : IDisposable
{
    private readonly IFontSubstitutor _substitutor;
    private readonly FontSubstitutionMap _substitutionMap;
    private readonly HashSet<string> _standardFonts;
    private readonly Dictionary<PdfSubstitutionInfo, SfntPdfTypeface> _familyTypefaces = [];
    private readonly Dictionary<PdfSubstitutionInfo, List<SfntPdfTypeface>> _characterFallbackTypefaces = [];
    private SfntPdfTypeface? _fallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontProvider"/> class.
    /// </summary>
    /// <param name="substitutor">Loads typefaces from the underlying font source.</param>
    /// <param name="substitutionMap">Names the families that stand in for each Standard 14 font.</param>
    public FontProvider(IFontSubstitutor substitutor, FontSubstitutionMap substitutionMap)
    {
        if (substitutor == null)
        {
            throw new ArgumentNullException(nameof(substitutor));
        }

        if (substitutionMap == null)
        {
            throw new ArgumentNullException(nameof(substitutionMap));
        }

        _substitutor = substitutor;
        _substitutionMap = substitutionMap;
        _standardFonts = new HashSet<string>(substitutionMap.EnumerateFamilyNames(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a typeface able to render <paramref name="unicode"/>: the requested family when it
    /// covers the text, then any available font that does, then the last-resort typeface.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints (stem, weight, width, slant).</param>
    /// <param name="unicode">The Unicode text the resolved typeface must be able to render.</param>
    /// <returns>A typeface to shape <paramref name="unicode"/> against; never <see langword="null"/>.</returns>
    public SfntPdfTypeface GetTypefaceByUnicode(in PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        SfntPdfTypeface? familyTypeface = ResolveFamilyByUnicode(substitutionInfo, unicode);
        if (familyTypeface != null)
        {
            return familyTypeface;
        }

        SfntPdfTypeface? characterTypeface = (!string.IsNullOrEmpty(unicode)) ? ResolveByCharacter(substitutionInfo, unicode) : null;

        return characterTypeface ?? GetSubstitutorFallbackTypeface();
    }

    /// <summary>
    /// Resolves the requested family when it addresses its glyphs by character code through a built-in
    /// symbol encoding - as Wingdings and its kin do - and maps <paramref name="characterCode"/> to a
    /// glyph. No Unicode value addresses such a glyph, so this is the only way to reach it.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints (stem, weight, width, slant).</param>
    /// <param name="characterCode">The PDF character code to look up in the family's built-in encoding.</param>
    /// <returns>The resolved typeface, or <see langword="null"/> when the family is not symbol-encoded or does not map the code.</returns>
    public SfntPdfTypeface? GetSymbolTypefaceByCode(in PdfSubstitutionInfo substitutionInfo, int characterCode)
        => ResolveSymbolFamilyByCode(substitutionInfo, characterCode);

    /// <summary>
    /// Returns the last-resort typeface to measure against when a character resolves to no glyph at all.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints, used to match style where possible.</param>
    public SfntPdfTypeface GetFallbackTypeface(in PdfSubstitutionInfo substitutionInfo)
    {
        string? fallbackFamilyName = _substitutionMap.FallbackFamilyName;
        if (fallbackFamilyName != null)
        {
            SfntPdfTypeface? resolved = ResolveByFamilyName(new PdfSubstitutionInfo(fallbackFamilyName, substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.ItalicAngle));
            if (resolved != null)
            {
                return resolved;
            }
        }

        return GetSubstitutorFallbackTypeface();
    }

    /// <summary>
    /// Disposes and discards every cached typeface that isn't one of the provider's core substitution
    /// targets - the Standard 14 family candidates and the configured fallback font - freeing memory
    /// held by transient Unicode-fallback resolutions.
    /// </summary>
    public void Cleanup()
    {
        List<PdfSubstitutionInfo> familyKeysToRemove = [];
        foreach (KeyValuePair<PdfSubstitutionInfo, SfntPdfTypeface> entry in _familyTypefaces)
        {
            if (_standardFonts.Contains(entry.Key.NormalizedStem))
            {
                continue;
            }

            entry.Value.Dispose();
            familyKeysToRemove.Add(entry.Key);
        }

        foreach (PdfSubstitutionInfo key in familyKeysToRemove)
        {
            _familyTypefaces.Remove(key);
        }

        foreach (List<SfntPdfTypeface> candidates in _characterFallbackTypefaces.Values)
        {
            foreach (SfntPdfTypeface typeface in candidates)
            {
                typeface.Dispose();
            }
        }

        _characterFallbackTypefaces.Clear();
    }

    /// <summary>
    /// Returns the font source's own last-resort typeface.
    /// </summary>
    private SfntPdfTypeface GetSubstitutorFallbackTypeface()
    {
        if (_fallback == null)
        {
            _fallback = _substitutor.GetFallbackTypeface();
        }

        return _fallback;
    }

    /// <summary>
    /// Walks the families that stand in for the requested one - the Standard 14 candidates first, then
    /// the requested family itself, then the configured fallback family - returning the first that
    /// covers <paramref name="unicode"/>.
    /// </summary>
    private SfntPdfTypeface? ResolveFamilyByUnicode(in PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        PdfStandardFontName? standardName = substitutionInfo.GetStandardName();
        if (standardName.HasValue && _substitutionMap.Candidates.TryGetValue(standardName.Value, out IReadOnlyList<string>? candidates) && candidates != null)
        {
            foreach (string candidate in candidates)
            {
                SfntPdfTypeface? resolvedCandidate = ResolveByFamilyName(new PdfSubstitutionInfo(candidate, substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.ItalicAngle));

                if (resolvedCandidate != null && resolvedCandidate.ContainsAllGlyphs(unicode))
                {
                    return resolvedCandidate;
                }
            }
        }

        SfntPdfTypeface? resolved = ResolveByFamilyName(substitutionInfo);

        if (resolved != null && resolved.ContainsAllGlyphs(unicode))
        {
            return resolved;
        }

        string? fallbackFamilyName = _substitutionMap.FallbackFamilyName;
        if (fallbackFamilyName != null)
        {
            SfntPdfTypeface? resolvedFallback = ResolveByFamilyName(new PdfSubstitutionInfo(fallbackFamilyName, substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.ItalicAngle));

            if (resolvedFallback != null && resolvedFallback.ContainsAllGlyphs(unicode))
            {
                return resolvedFallback;
            }
        }

        return null;
    }

    /// <summary>
    /// Walks the families that stand in for the requested one - the Standard 14 candidates first, then
    /// the requested family itself - returning the first that is symbol-encoded and maps
    /// <paramref name="characterCode"/>.
    /// </summary>
    private SfntPdfTypeface? ResolveSymbolFamilyByCode(in PdfSubstitutionInfo substitutionInfo, int characterCode)
    {
        PdfStandardFontName? standardName = substitutionInfo.GetStandardName();
        if (standardName.HasValue && _substitutionMap.Candidates.TryGetValue(standardName.Value, out IReadOnlyList<string>? candidates) && candidates != null)
        {
            foreach (string candidate in candidates)
            {
                SfntPdfTypeface? resolvedCandidate = ResolveByFamilyName(new PdfSubstitutionInfo(candidate, substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.ItalicAngle));

                if (resolvedCandidate != null && resolvedCandidate.IsSymbolEncoded && resolvedCandidate.GetGidByCode(characterCode) != null)
                {
                    return resolvedCandidate;
                }
            }
        }

        SfntPdfTypeface? resolved = ResolveByFamilyName(substitutionInfo);

        if (resolved != null && resolved.IsSymbolEncoded && resolved.GetGidByCode(characterCode) != null)
        {
            return resolved;
        }

        return null;
    }

    private SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo)
    {
        if (_familyTypefaces.TryGetValue(substitutionInfo, out SfntPdfTypeface? cached))
        {
            return cached;
        }

        SfntPdfTypeface? typeface = _substitutor.ResolveByFamilyName(substitutionInfo);
        if (typeface != null)
        {
            _familyTypefaces[substitutionInfo] = typeface;
        }

        return typeface;
    }

    private SfntPdfTypeface? ResolveByCharacter(in PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        if (_characterFallbackTypefaces.TryGetValue(substitutionInfo, out List<SfntPdfTypeface>? candidates))
        {
            foreach (SfntPdfTypeface candidate in candidates)
            {
                if (candidate.ContainsAllGlyphs(unicode))
                {
                    return candidate;
                }
            }
        }

        SfntPdfTypeface? typeface = _substitutor.ResolveByCharacter(substitutionInfo, unicode);
        if (typeface != null)
        {
            if (candidates == null)
            {
                candidates = new List<SfntPdfTypeface>();
                _characterFallbackTypefaces[substitutionInfo] = candidates;
            }

            candidates.Add(typeface);
        }

        return typeface;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (SfntPdfTypeface typeface in _familyTypefaces.Values)
        {
            typeface.Dispose();
        }

        foreach (List<SfntPdfTypeface> candidates in _characterFallbackTypefaces.Values)
        {
            foreach (SfntPdfTypeface typeface in candidates)
            {
                typeface.Dispose();
            }
        }

        _fallback?.Dispose();

        _familyTypefaces.Clear();
        _characterFallbackTypefaces.Clear();
    }
}
