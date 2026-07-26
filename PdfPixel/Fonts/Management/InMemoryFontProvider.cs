using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Fonts.Management;

// TODO: [HIGH] InMemoryFontProvider must register fonts with style, otherwise we can't find a match

/// <summary>
/// Font provider that resolves standard PDF fonts and named fonts from explicitly registered in-memory font data.
/// Suitable for environments where system fonts are unavailable, such as browser/WASM.
/// </summary>
public sealed class InMemoryFontProvider : IFontProvider
{
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

    private static readonly SfntPdfTypefaceParameters SubstitutedTypefaceParameters = new() { RepackTypeface = false };

    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<PdfStandardFontName, SfntPdfTypeface> _standardFonts = [];
    private readonly Dictionary<string, SfntPdfTypeface> _namedFonts = new(StringComparer.OrdinalIgnoreCase);
    private SfntPdfTypeface? _fallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryFontProvider"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during font parsing.</param>
    public InMemoryFontProvider(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    /// <summary>
    /// Registers font data to use as the fallback typeface when no registered font matches a requested name or glyph.
    /// </summary>
    /// <param name="fontData">Raw SFNT font program bytes.</param>
    public void RegisterFallback(in ReadOnlyMemory<byte> fontData)
    {
        MemoryStream fontStream = new(fontData.ToArray());
        _fallback = new SfntPdfTypeface(fontStream, _loggerFactory, SubstitutedTypefaceParameters);
    }

    /// <summary>
    /// Registers font data for a standard PDF font name.
    /// The typeface is also registered under the standard display names so that <see cref="GetTypeface"/>
    /// can resolve it by common name strings found in PDF documents.
    /// </summary>
    /// <param name="standardFont">The standard PDF font to associate with the supplied font data.</param>
    /// <param name="fontData">Raw SFNT font program bytes.</param>
    public void RegisterStandardFont(PdfStandardFontName standardFont, in ReadOnlyMemory<byte> fontData)
    {
        MemoryStream fontStream = new(fontData.ToArray());
        SfntPdfTypeface typeface = new(fontStream, _loggerFactory, SubstitutedTypefaceParameters);
        _standardFonts[standardFont] = typeface;

        if (StandardFontDisplayNames.TryGetValue(standardFont, out string[]? displayNames))
        {
            for (int i = 0; i < displayNames.Length; i++)
            {
                _namedFonts[displayNames[i]] = typeface;
            }
        }
    }

    /// <summary>
    /// Registers font data under an explicit family name.
    /// </summary>
    /// <param name="name">The family name PDF documents use to reference this font.</param>
    /// <param name="fontData">Raw SFNT font program bytes.</param>
    public void RegisterFont(string name, in ReadOnlyMemory<byte> fontData)
    {
        MemoryStream fontStream = new(fontData.ToArray());
        _namedFonts[name] = new SfntPdfTypeface(fontStream, _loggerFactory, SubstitutedTypefaceParameters);
    }

    /// <inheritdoc/>
    public SfntPdfTypeface GetTypeface(in PdfSubstitutionInfo substitutionInfo, string? unicode, float? width)
    {
        PdfStandardFontName? standardName = substitutionInfo.GetStandardName();
        if (standardName.HasValue
            && _standardFonts.TryGetValue(standardName.Value, out SfntPdfTypeface? standardTypeface)
            && standardTypeface.ContainsAllGlyphs(unicode))
        {
            return standardTypeface;
        }

        if (_namedFonts.TryGetValue(substitutionInfo.NormalizedStem, out SfntPdfTypeface? namedTypeface) && namedTypeface.ContainsAllGlyphs(unicode))
        {
            return namedTypeface;
        }

        return _fallback ?? throw new InvalidOperationException("No fallback font has been registered.");
    }

    /// <inheritdoc/>
    public void Cleanup()
    {
        // No-op: every typeface here was explicitly registered, not resolved transiently.
    }

    /// <summary>
    /// Clears internal font registrations.
    /// </summary>
    public void Dispose()
    {
        foreach (SfntPdfTypeface typeface in _standardFonts.Values)
        {
            typeface.Dispose();
        }

        foreach (SfntPdfTypeface typeface in _namedFonts.Values)
        {
            typeface.Dispose();
        }

        _fallback?.Dispose();

        _standardFonts.Clear();
        _namedFonts.Clear();
    }
}
