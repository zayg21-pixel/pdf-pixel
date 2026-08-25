using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Locates a typeface to substitute for a PDF font whose own program is unavailable and which is not
/// one of the Standard 14 - those are served from this assembly's own resources and never reach here.
/// </summary>
public sealed class FontProvider : IDisposable
{
    private readonly IFontSubstitutor _substitutor;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<PdfSubstitutionInfo, SfntPdfTypeface> _familyTypefaces = [];
    private readonly Dictionary<PdfSubstitutionInfo, List<SfntPdfTypeface>> _characterFallbackTypefaces = [];
    private SfntPdfTypeface? _fallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontProvider"/> class.
    /// </summary>
    /// <param name="substitutor">Loads typefaces from the underlying font source.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public FontProvider(IFontSubstitutor substitutor, ILoggerFactory loggerFactory)
    {
        if (substitutor == null)
        {
            throw new ArgumentNullException(nameof(substitutor));
        }

        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _substitutor = substitutor;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Gets the font source every substituted typeface this provider hands out was loaded from.
    /// </summary>
    public IFontSubstitutor Substitutor => _substitutor;

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
    {
        SfntPdfTypeface? resolved = ResolveByFamilyName(substitutionInfo);

        if (resolved != null && resolved.IsSymbolEncoded && resolved.GetGidByCode(characterCode) != null)
        {
            return resolved;
        }

        return null;
    }

    /// <summary>
    /// Returns the last-resort typeface to measure against when a character resolves to no glyph at all.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints, used to match style where possible.</param>
    public SfntPdfTypeface GetFallbackTypeface(in PdfSubstitutionInfo substitutionInfo) => GetStandard14Fallback(substitutionInfo);

    /// <summary>
    /// Disposes and discards every typeface cached here, freeing the memory held by resolutions this
    /// provider owns. The Standard 14 resources are not among them: they are shared process-wide and
    /// never held in these caches.
    /// </summary>
    public void Cleanup()
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

        _familyTypefaces.Clear();
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
    /// Returns the embedded Standard 14 Helvetica in the requested style. The instance is shared
    /// process-wide, so it is never cached here and never disposed.
    /// </summary>
    private SfntPdfTypeface GetStandard14Fallback(in PdfSubstitutionInfo substitutionInfo)
        => Standard14TypefaceLoader.GetTypeface(PdfFontStandardName.Helvetica, substitutionInfo.IsBold, substitutionInfo.IsItalic, _loggerFactory);

    /// <summary>
    /// Walks the families that stand in for the requested one - the requested family itself, then the
    /// embedded Standard 14 fallback - returning the first that covers <paramref name="unicode"/>.
    /// </summary>
    private SfntPdfTypeface? ResolveFamilyByUnicode(in PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        SfntPdfTypeface? resolved = ResolveByFamilyName(substitutionInfo);

        if (resolved != null && resolved.ContainsAllGlyphs(unicode))
        {
            return resolved;
        }

        SfntPdfTypeface standard14Fallback = GetStandard14Fallback(substitutionInfo);

        return (standard14Fallback.ContainsAllGlyphs(unicode)) ? standard14Fallback : null;
    }

    /// <summary>
    /// Resolves the requested family to a font installed under that name, or <see langword="null"/>
    /// when no such family is installed. A substitute the platform offers in its place is never
    /// returned, so the result is the family the document names or nothing.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints (stem, weight, width, slant).</param>
    public SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo)
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
        Cleanup();

        _fallback?.Dispose();
        _fallback = null;
    }
}
