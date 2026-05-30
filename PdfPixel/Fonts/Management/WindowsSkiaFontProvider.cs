using System;
using SkiaSharp;
using PdfPixel.Fonts.Mapping;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Windows-specific Skia font provider that resolves standard PDF fonts and named fonts using system-installed families.
/// </summary>
public sealed class WindowsSkiaFontProvider : ISkiaFontProvider, IDisposable
{
    private readonly SKFontManager _fontManager;
    private readonly string? _fallbackFontName;

    private static readonly Dictionary<PdfStandardFontName, string[]> CandidatesMap = new()
    {
        { PdfStandardFontName.Times, new[] { "Times New Roman" } },
        { PdfStandardFontName.TimesNewRoman, new[] { "Times New Roman" } },
        { PdfStandardFontName.TimesNewRomanPS, new[] { "Times New Roman" } },
        { PdfStandardFontName.Helvetica, new[] { "Arial" } },
        { PdfStandardFontName.Arial, new[] { "Arial" } },
        { PdfStandardFontName.Courier, new[] { "Courier New" } },
        { PdfStandardFontName.CourierNew, new[] { "Courier New" } },
        { PdfStandardFontName.CourierNewPS, new[] { "Courier New" } },
        { PdfStandardFontName.Symbol, new[] { "Segoe UI Symbol", "Times New Roman" } },
        { PdfStandardFontName.ZapfDingbats, new[] { "Segoe UI Symbol" } }
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
    public SKTypeface? GetStandardFont(PdfStandardFontName standardFont, SKFontStyle style, string? unicode)
    {
        if (!CandidatesMap.TryGetValue(standardFont, out string[]? candidates))
        {
            return null;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            SKTypeface matchedTypeface = _fontManager.MatchFamily(candidates[i], style);
            if (matchedTypeface != null && (unicode == null || matchedTypeface.ContainsGlyphs(unicode)))
            {
                return matchedTypeface;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public SKTypeface GetFont(string? name, SKFontStyle style, string? unicode)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = _fallbackFontName;
        }

        SKTypeface matchedTypeface = _fontManager.MatchFamily(name, style);
        if (matchedTypeface != null && (unicode == null || matchedTypeface.ContainsGlyphs(unicode)))
        {
            return matchedTypeface;
        }

        if (string.IsNullOrEmpty(unicode))
        {
            return _fontManager.MatchFamily(_fallbackFontName, style);
        }

        if (unicode != null && unicode.Length > 0)
        {
            return _fontManager.MatchCharacter(_fallbackFontName, style, default, unicode[0]);
        }

        return _fontManager.MatchFamily(_fallbackFontName);
    }

    /// <inheritdoc/>
    public void Dispose() => _fontManager?.Dispose();
}
