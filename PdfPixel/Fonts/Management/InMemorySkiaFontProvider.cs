using System;
using System.Collections.Generic;
using PdfPixel.Fonts.Mapping;
using SkiaSharp;

namespace PdfPixel.Fonts.Management;

// TODO: [HIGH] InMemorySkiaFontProvider must register fonts with style, otherwise we can't find a match

/// <summary>
/// Font provider that resolves standard PDF fonts and named fonts from explicitly registered in-memory font data.
/// Suitable for environments where system fonts are unavailable, such as browser/WASM.
/// </summary>
public sealed class InMemorySkiaFontProvider : ISkiaFontProvider
{
    private readonly Dictionary<PdfStandardFontName, PdfTypeface> _standardFonts = [];
    private readonly Dictionary<string, PdfTypeface> _namedFonts = new(StringComparer.OrdinalIgnoreCase);
    private PdfTypeface _fallback;

    /// <summary>
    /// Maps each <see cref="PdfStandardFontName"/> to the common display names that PDF documents
    /// may use when referencing the font by family name string.
    /// </summary>
    private static readonly Dictionary<PdfStandardFontName, string[]> StandardFontDisplayNames = new()
    {
        { PdfStandardFontName.Times, ["Times"] },
        { PdfStandardFontName.TimesNewRoman, ["Times New Roman", "TimesNewRomanPSMT"] },
        { PdfStandardFontName.TimesNewRomanPS, ["TimesNewRomanPS", "TimesNewRomanPSMT"] },
        { PdfStandardFontName.Helvetica, ["Helvetica"] },
        { PdfStandardFontName.Arial, ["Arial"] },
        { PdfStandardFontName.Courier, ["Courier"] },
        { PdfStandardFontName.CourierNew, ["Courier New"] },
        { PdfStandardFontName.CourierNewPS, ["CourierNewPS", "CourierNewPSMT"] },
        { PdfStandardFontName.Symbol, ["Symbol"] },
        { PdfStandardFontName.ZapfDingbats, ["ZapfDingbats"] }
    };

    /// <summary>
    /// Initializes a new instance of <see cref="InMemorySkiaFontProvider"/> with the Skia default typeface as fallback.
    /// </summary>
    public InMemorySkiaFontProvider() => _fallback = new PdfTypeface(SKTypeface.Default);

    /// <summary>
    /// Registers font data to use as the fallback typeface when no registered font matches a requested name or glyph.
    /// </summary>
    /// <param name="fontData">Raw font file bytes (TTF, OTF, etc.).</param>
    public void RegisterFallback(byte[] fontData) => _fallback = new PdfTypeface(fontData);

    /// <summary>
    /// Registers font data for a standard PDF font name.
    /// The typeface is also registered under its own family name and the standard display names
    /// so that <see cref="GetFont"/> can resolve it by common name strings found in PDF documents.
    /// </summary>
    /// <param name="standardFont">The standard PDF font to associate with the supplied font data.</param>
    /// <param name="fontData">Raw font file bytes (TTF, OTF, etc.).</param>
    public void RegisterStandardFont(PdfStandardFontName standardFont, byte[] fontData)
    {
        PdfTypeface typeface = new(fontData);
        _standardFonts[standardFont] = typeface;

        // Register by the typeface's own family name so GetFont can match it
        string? familyName = typeface.GetTypeface().FamilyName;
        if (!string.IsNullOrEmpty(familyName))
        {
            _namedFonts[familyName] = typeface;
        }

        // Register by well-known display names that PDFs commonly reference
        if (StandardFontDisplayNames.TryGetValue(standardFont, out string[]? displayNames))
        {
            for (int i = 0; i < displayNames.Length; i++)
            {
                _namedFonts[displayNames[i]] = typeface;
            }
        }
    }

    /// <inheritdoc/>
    public PdfTypeface? GetStandardFont(PdfStandardFontName standardFont, SKFontStyle style, string? unicode, float? width)
    {
        if (_standardFonts.TryGetValue(standardFont, out PdfTypeface? typeface) && typeface.ContainsGlyph(unicode))
        {
            return typeface;
        }

        return null;
    }

    /// <inheritdoc/>
    public PdfTypeface GetFont(string? name, SKFontStyle style, string? unicode, float? width)
    {
        if (name != null && _namedFonts.TryGetValue(name, out PdfTypeface? typeface) && typeface.ContainsGlyph(unicode))
        {
            return typeface;
        }

        return _fallback;
    }

    /// <summary>
    /// Clears internal font registrations. Underlying native typefaces are released via finalizer.
    /// </summary>
    public void Dispose()
    {
        _standardFonts.Clear();
        _namedFonts.Clear();
    }
}
