using System;
using System.Collections.Generic;
using System.IO;
using PdfPixel.Fonts.Mapping;
using SkiaSharp;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Font provider that resolves standard PDF fonts and named fonts from explicitly registered in-memory font data.
/// Suitable for environments where system fonts are unavailable, such as browser/WASM.
/// </summary>
public sealed class InMemorySkiaFontProvider : ISkiaFontProvider
{
    private readonly Dictionary<PdfStandardFontName, SKTypeface> _standardFonts = new();
    private readonly Dictionary<string, SKTypeface> _namedFonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<SKTypeface> _ownedTypefaces = new();
    private SKTypeface _fallback;

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
        { PdfStandardFontName.ZapfDingbats, ["ZapfDingbats"] },
    };

    /// <summary>
    /// Registers font data for a standard PDF font name.
    /// The typeface is also registered under its own family name and the standard display names
    /// so that <see cref="GetFont"/> can resolve it by common name strings found in PDF documents.
    /// </summary>
    /// <param name="standardFont">The standard PDF font to associate with the supplied font data.</param>
    /// <param name="fontData">Raw font file bytes (TTF, OTF, etc.).</param>
    public void RegisterStandardFont(PdfStandardFontName standardFont, byte[] fontData)
    {
        var typeface = SKTypeface.FromStream(new MemoryStream(fontData));
        if (typeface == null)
        {
            return;
        }

        _ownedTypefaces.Add(typeface);
        _standardFonts[standardFont] = typeface;

        // Register by the typeface's own family name so GetFont can match it
        if (!string.IsNullOrEmpty(typeface.FamilyName))
        {
            _namedFonts[typeface.FamilyName] = typeface;
        }

        // Register by well-known display names that PDFs commonly reference
        if (StandardFontDisplayNames.TryGetValue(standardFont, out var displayNames))
        {
            for (int i = 0; i < displayNames.Length; i++)
            {
                _namedFonts[displayNames[i]] = typeface;
            }
        }

        // Use the first registered font as the fallback
        _fallback ??= typeface;
    }

    /// <inheritdoc/>
    public SKTypeface GetStandardFont(PdfStandardFontName standardFont, SKFontStyle style, string unicode)
    {
        if (_standardFonts.TryGetValue(standardFont, out var typeface))
        {
            if (unicode == null || typeface.ContainsGlyphs(unicode))
            {
                return typeface;
            }
        }

        return _fallback;
    }

    /// <inheritdoc/>
    public SKTypeface GetFont(string name, SKFontStyle style, string unicode)
    {
        if (!string.IsNullOrEmpty(name) && _namedFonts.TryGetValue(name, out var typeface))
        {
            if (unicode == null || typeface.ContainsGlyphs(unicode))
            {
                return typeface;
            }
        }

        return _fallback;
    }

    /// <summary>
    /// Disposes all owned typeface instances and clears internal registrations.
    /// </summary>
    public void Dispose()
    {
        foreach (var typeface in _ownedTypefaces)
        {
            typeface.Dispose();
        }

        _standardFonts.Clear();
        _namedFonts.Clear();
        _ownedTypefaces.Clear();
        _fallback = null;
    }
}
