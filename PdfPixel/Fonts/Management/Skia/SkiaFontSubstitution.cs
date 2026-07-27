using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Typeface;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management.Skia;

/// <summary>
/// Platform-agnostic helpers for resolving an installed system font via Skia's own font manager
/// (<see cref="SKFontManager"/>), which on each platform delegates to that platform's native font
/// substitution mechanism (DirectWrite on Windows, CoreText on macOS, fontconfig on Linux). The
/// resolved font's program bytes are read lazily from Skia's own font data, without buffering into
/// managed memory up front.
/// </summary>
internal sealed class SkiaFontSubstitution
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly HashSet<string> _knownFontFamilies;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaFontSubstitution"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public SkiaFontSubstitution(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _knownFontFamilies = new HashSet<string>(SKFontManager.Default.FontFamilies, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether <paramref name="name"/> names an installed font family.
    /// </summary>
    /// <param name="name">The font family name to check.</param>
    public bool IsKnownFamilyName(string name) => _knownFontFamilies.Contains(name);

    /// <summary>
    /// Resolves the font named by <paramref name="substitutionInfo"/>, or <see langword="null"/> if
    /// it isn't installed or - when <paramref name="unicode"/> is given - doesn't have a glyph for its
    /// first codepoint.
    /// </summary>
    /// <param name="substitutionInfo">Names the font family and style to resolve.</param>
    /// <param name="unicode">The Unicode text whose first codepoint the resolved font must cover, or <see langword="null"/> to skip that check.</param>
    public SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo, string? unicode)
    {
        SKFontStyle style = CreateFontStyle(substitutionInfo.IsBold, substitutionInfo.IsItalic);

        using SKTypeface? matchedTypeface = SKFontManager.Default.MatchFamily(substitutionInfo.NormalizedStem, style);
        if (matchedTypeface == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(unicode))
        {
            int codepoint = char.ConvertToUtf32(unicode, 0);
            using SKFont font = new(matchedTypeface);
            if (font.GetGlyph(codepoint) == 0)
            {
                return null;
            }
        }

        return BuildTypeface(matchedTypeface);
    }

    /// <summary>
    /// Resolves the installed font family best able to render the first codepoint in
    /// <paramref name="unicode"/>, preferring <paramref name="substitutionInfo"/>'s family name.
    /// </summary>
    /// <param name="substitutionInfo">Names the preferred font family and style to resolve.</param>
    /// <param name="unicode">The Unicode text whose first codepoint to resolve a font for.</param>
    /// <returns>The resolved typeface, or <see langword="null"/> if no installed font can render it.</returns>
    public SfntPdfTypeface? ResolveByCharacter(in PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        int codepoint = char.ConvertToUtf32(unicode, 0);
        SKFontStyle style = CreateFontStyle(substitutionInfo.IsBold, substitutionInfo.IsItalic);

        using SKTypeface? matchedTypeface = SKFontManager.Default.MatchCharacter(
            substitutionInfo.NormalizedStem,
            style.Weight,
            style.Width,
            style.Slant,
            null,
            codepoint);

        return BuildTypeface(matchedTypeface);
    }

    /// <summary>
    /// Resolves Skia's own default typeface, used as the final-resort font when nothing else matches.
    /// </summary>
    public SfntPdfTypeface GetFallbackFont()
        => BuildTypeface(SKTypeface.Default) ?? throw new InvalidOperationException("Could not load Skia's default typeface.");

    private static SKFontStyle CreateFontStyle(bool isBold, bool isItalic)
    {
        SKFontStyleWeight weight = isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        SKFontStyleSlant slant = isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
    }

    private SfntPdfTypeface? BuildTypeface(SKTypeface? typeface)
    {
        if (typeface == null)
        {
            return null;
        }

        SKStreamAsset? streamAsset = typeface.OpenStream(out int ttcIndex);
        if (streamAsset == null)
        {
            return null;
        }

        SfntPdfTypefaceParameters parameters = new() { RepackTypeface = false, TtcIndex = ttcIndex };
        return new SfntPdfTypeface(new SkStreamAssetStream(streamAsset), _loggerFactory, parameters);
    }
}
