using System;
using SkiaSharp;
using PdfPixel.Fonts.Mapping;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Windows-specific Skia font provider that resolves standard PDF fonts and named fonts using system-installed families.
/// </summary>
public sealed class WindowsSkiaFontProvider : ISkiaFontProvider
{
    private readonly SKFontManager _fontManager;
    private readonly string? _fallbackFontName;

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

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsSkiaFontProvider"/> using the default system font manager.
    /// </summary>
    /// <param name="fallbackFontName">
    /// Optional family name of the typeface to use when no match is found for a requested font name or glyph.
    /// When <see langword="null"/>, Skia's default typeface selection is used.
    /// </param>
    public WindowsSkiaFontProvider(string? fallbackFontName = null)
    {
        _fallbackFontName = fallbackFontName;
        _fontManager = SKFontManager.CreateDefault();
    }

    /// <inheritdoc/>
    public PdfTypeface? GetStandardFont(PdfStandardFontName standardFont, SKFontStyle style, string? unicode, float? width)
    {
        if (!CandidatesMap.TryGetValue(standardFont, out string[]? candidates))
        {
            return null;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            SKTypeface matchedTypeface = _fontManager.MatchFamily(candidates[i], style);
            if (matchedTypeface != null)
            {
                PdfTypeface candidate = new(matchedTypeface);
                if (candidate.ContainsGlyph(unicode))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public PdfTypeface GetFont(string? name, SKFontStyle style, string? unicode, float? width)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = _fallbackFontName;
        }

        SKTypeface matchedTypeface = _fontManager.MatchFamily(name, style);
        if (matchedTypeface != null)
        {
            PdfTypeface candidate = new(matchedTypeface);
            if (candidate.ContainsGlyph(unicode))
            {
                return candidate;
            }
        }

        if (string.IsNullOrEmpty(unicode))
        {
            return new PdfTypeface(_fontManager.MatchFamily(_fallbackFontName, style));
        }

        if (unicode != null && unicode.Length > 0)
        {
            return new PdfTypeface(_fontManager.MatchCharacter(_fallbackFontName, style, default, unicode[0]));
        }

        return new PdfTypeface(_fontManager.MatchFamily(_fallbackFontName));
    }

    /// <inheritdoc/>
    public void Dispose() => _fontManager?.Dispose();
}
