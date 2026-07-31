using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management.Skia;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Windows-specific font provider that resolves standard PDF fonts, named fonts, and Unicode
/// fallback fonts via Skia's own font manager, which on Windows delegates to DirectWrite.
/// </summary>
public sealed class WindowsFontProvider : IFontProvider
{
    private static readonly Dictionary<PdfStandardFontName, string[]> CandidatesMap = new()
    {
        { PdfStandardFontName.Times, ["Times New Roman"] },
        { PdfStandardFontName.TimesNewRoman, ["Times New Roman"] },
        { PdfStandardFontName.TimesNewRomanPS, ["Times New Roman"] },
        { PdfStandardFontName.Helvetica, ["Arial"] },
        { PdfStandardFontName.Arial, ["Arial"] },
        { PdfStandardFontName.Courier, ["Courier New"] },
        { PdfStandardFontName.CourierNew, ["Courier New"] },
        { PdfStandardFontName.CourierNewPS, ["Courier New"] },
        { PdfStandardFontName.Symbol, ["Segoe UI Symbol", "Times New Roman"] },
        { PdfStandardFontName.ZapfDingbats, ["Segoe UI Symbol"] }
    };

    private readonly SkiaFontSubstitution _skiaFontSubstitution;
    private readonly Dictionary<PdfSubstitutionInfo, SfntPdfTypeface> _familyTypefaces = [];
    private readonly Dictionary<PdfSubstitutionInfo, List<SfntPdfTypeface>> _characterFallbackTypefaces = [];
    private readonly string _fallbackFontName;
    private readonly SfntPdfTypeface _fallback;

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsFontProvider"/>. Standard 14, named, and fallback
    /// fonts are all loaded lazily, on first request.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during font parsing.</param>
    /// <param name="fallbackFontName">Family name of the typeface to use when no match is found for a requested font name or glyph.</param>
    public WindowsFontProvider(ILoggerFactory loggerFactory, string fallbackFontName = "Arial")
    {
        _skiaFontSubstitution = new SkiaFontSubstitution(loggerFactory);
        _fallbackFontName = fallbackFontName;
        _fallback = _skiaFontSubstitution.GetFallbackFont();
    }

    /// <inheritdoc/>
    public SfntPdfTypeface GetTypeface(in PdfSubstitutionInfo substitutionInfo, string? unicode, float? width)
    {
        PdfStandardFontName? standardName = substitutionInfo.GetStandardName();
        if (standardName.HasValue && CandidatesMap.TryGetValue(standardName.Value, out string[]? candidates) && candidates != null)
        {
            foreach (string candidate in candidates)
            {
                SfntPdfTypeface? resolved = ResolveByFamilyName(new PdfSubstitutionInfo(candidate, substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.ItalicAngle), unicode);

                if (resolved != null)
                {
                    return resolved;
                }
            }
        }

        if (_skiaFontSubstitution.IsKnownFamilyName(substitutionInfo.NormalizedStem))
        {
            SfntPdfTypeface? resolved = ResolveByFamilyName(substitutionInfo, unicode);

            if (resolved != null)
            {
                return resolved;
            }
        }

        if (unicode != null && unicode.Length > 0)
        {
            SfntPdfTypeface? resolved = ResolveByCharacter(substitutionInfo, unicode);

            if (resolved != null)
            {
                return resolved;
            }
        }


        SfntPdfTypeface? resolvedfallback = ResolveByFamilyName(new PdfSubstitutionInfo(_fallbackFontName, substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.ItalicAngle), unicode);
        return resolvedfallback ?? _fallback;
    }

    private SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo, string? unicode)
    {
        if (_familyTypefaces.TryGetValue(substitutionInfo, out SfntPdfTypeface? cached))
        {
            return (cached.ContainsAllGlyphs(unicode)) ? cached : null;
        }

        SfntPdfTypeface? typeface = _skiaFontSubstitution.ResolveByFamilyName(substitutionInfo, unicode);
        if (typeface != null)
        {
            _familyTypefaces[substitutionInfo] = typeface;
        }

        return typeface;
    }

    private SfntPdfTypeface? ResolveByCharacter(in PdfSubstitutionInfo substitutionInfo, string? unicode)
    {
        if (unicode == null || unicode.Length == 0)
        {
            return null;
        }

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

        SfntPdfTypeface? typeface = _skiaFontSubstitution.ResolveByCharacter(substitutionInfo, unicode);
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

    /// <inheritdoc/>
    public void Cleanup()
    {
        HashSet<string> protectedFamilyNames = new(StringComparer.OrdinalIgnoreCase) { _fallbackFontName };
        foreach (string[] candidates in CandidatesMap.Values)
        {
            foreach (string candidate in candidates)
            {
                protectedFamilyNames.Add(candidate);
            }
        }

        List<PdfSubstitutionInfo> familyKeysToRemove = [];
        foreach (KeyValuePair<PdfSubstitutionInfo, SfntPdfTypeface> entry in _familyTypefaces)
        {
            if (protectedFamilyNames.Contains(entry.Key.NormalizedStem))
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

    /// <inheritdoc/>
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

        _fallback.Dispose();

        _familyTypefaces.Clear();
        _characterFallbackTypefaces.Clear();
    }
}
