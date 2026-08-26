using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Skia.Fonts;

/// <summary>
/// Loads installed system fonts through Skia's <see cref="SKFontManager"/>.
/// </summary>
public sealed class SkiaFontSubstitutor : IFontSubstitutor
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly HashSet<string> _knownFontFamilies;
    private readonly Dictionary<string, string> _postscriptFamilies = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaFontSubstitutor"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public SkiaFontSubstitutor(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _knownFontFamilies = new HashSet<string>(SKFontManager.Default.FontFamilies, StringComparer.OrdinalIgnoreCase);

        foreach (string family in _knownFontFamilies)
        {
            using SKTypeface skiaTypeface = SKFontManager.Default.MatchFamily(family);

            string postscriptName = skiaTypeface.PostScriptName;

            if (postscriptName.EndsWith("MT"))
            {
                postscriptName = postscriptName.Substring(0, postscriptName.Length - 2);
            }

            _postscriptFamilies.Add(postscriptName, family);
        }
    }

    /// <inheritdoc />
    public SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo)
    {
        if (!TryResolveFamilyName(substitutionInfo.NormalizedStem, out string? familyName) || familyName == null)
        {
            return null;
        }

        SKFontStyle style = CreateFontStyle(substitutionInfo.Weight, substitutionInfo.Width, substitutionInfo.IsItalic);

        using SKTypeface? matchedTypeface = SKFontManager.Default.MatchFamily(familyName, style);
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

    private bool TryResolveFamilyName(string fontName, out string? resolvedName)
    {
        if (_knownFontFamilies.Contains(fontName))
        {
            resolvedName = fontName;
            return true;
        }

        if (_postscriptFamilies.TryGetValue(fontName, out string? familyName))
        {
            resolvedName = familyName;
            return true;
        }

        resolvedName = null;
        return false;
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
