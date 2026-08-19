using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Skia.Fonts;

/// <summary>
/// Loads installed system fonts through Skia's own font manager (<see cref="SKFontManager"/>), which
/// on each platform delegates to that platform's native font substitution mechanism (DirectWrite on
/// Windows, CoreText on macOS, fontconfig on Linux). The resolved font's program bytes are read
/// lazily from Skia's own font data, without buffering into managed memory up front.
/// </summary>
public sealed class SkiaFontSubstitutor : IFontSubstitutor
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly HashSet<string> _knownFontFamilies;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaFontSubstitutor"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public SkiaFontSubstitutor(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _knownFontFamilies = new HashSet<string>(SKFontManager.Default.FontFamilies, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Skia's <see cref="SKFontManager.MatchFamily(string, SKFontStyle)"/> answers an uninstalled family
    /// with its own idea of a substitute rather than with nothing, so the family name is checked against
    /// the installed set first.
    /// </remarks>
    public SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo)
    {
        if (!_knownFontFamilies.Contains(substitutionInfo.NormalizedStem))
        {
            return null;
        }

        SKFontStyle style = CreateFontStyle(substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.IsItalic);

        using SKTypeface? matchedTypeface = SKFontManager.Default.MatchFamily(substitutionInfo.NormalizedStem, style);
        if (matchedTypeface == null)
        {
            return null;
        }

        return BuildTypeface(matchedTypeface);
    }

    /// <inheritdoc />
    public SfntPdfTypeface? ResolveByCharacter(in PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        int codepoint = char.ConvertToUtf32(unicode, 0);
        SKFontStyle style = CreateFontStyle(substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.IsItalic);

        using SKTypeface? matchedTypeface = SKFontManager.Default.MatchCharacter(
            substitutionInfo.NormalizedStem,
            style.Weight,
            style.Width,
            style.Slant,
            null,
            codepoint);

        return BuildTypeface(matchedTypeface);
    }

    /// <inheritdoc />
    public SfntPdfTypeface GetFallbackTypeface()
        => BuildTypeface(SKTypeface.Default) ?? throw new InvalidOperationException("Could not load Skia's default typeface.");

    /// <summary>
    /// Loads the installed font <paramref name="metrics"/> names, in the style it describes.
    /// </summary>
    /// <param name="metrics">Names the font family and style to load.</param>
    /// <returns>The matched typeface, or <see langword="null"/> when the family is not installed.</returns>
    public SKTypeface? MatchSystemTypeface(PdfFontMetrics metrics)
    {
        if (metrics == null)
        {
            throw new ArgumentNullException(nameof(metrics));
        }

        string familyName = metrics.FamilyName.ToString();
        if (!_knownFontFamilies.Contains(familyName))
        {
            return null;
        }

        SKFontStyle style = CreateFontStyle(metrics.Weight, metrics.Width, metrics.IsItalic);

        return SKFontManager.Default.MatchFamily(familyName, style);
    }

    private static SKFontStyle CreateFontStyle(int weight, int width, bool isItalic)
    {
        SKFontStyleSlant slant = isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return new SKFontStyle((SKFontStyleWeight)weight, (SKFontStyleWidth)width, slant);
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

        SfntPdfTypefaceParameters parameters = new() { RepackTypeface = false, TtcIndex = ttcIndex, IsSystemFont = true };
        return new SfntPdfTypeface(new SkStreamAssetStream(streamAsset), _loggerFactory, parameters);
    }
}
