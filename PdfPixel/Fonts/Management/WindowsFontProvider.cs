using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Windows-specific font provider that resolves standard PDF fonts and named fonts by loading
/// installed font files directly from disk.
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

    private readonly ILoggerFactory _loggerFactory;
    private readonly WindowsFontDirectoryIndex _fontDirectoryIndex;
    private readonly Dictionary<PdfSubstitutionInfo, IPdfTypeface> _standardFonts = [];
    private readonly Dictionary<PdfSubstitutionInfo, IPdfTypeface> _namedFonts = [];
    private readonly IPdfTypeface _fallback;

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsFontProvider"/>, eagerly loading every Standard 14
    /// font family in all four style combinations from the installed Windows fonts.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during font parsing.</param>
    /// <param name="fallbackFontName">Family name of the typeface to use when no match is found for a requested font name or glyph.</param>
    public WindowsFontProvider(ILoggerFactory loggerFactory, string fallbackFontName = "Arial")
    {
        _loggerFactory = loggerFactory;
        _fontDirectoryIndex = new WindowsFontDirectoryIndex(loggerFactory);

        foreach (KeyValuePair<PdfStandardFontName, string[]> entry in CandidatesMap)
        {
            LoadAndStoreStandardFont(entry.Key, entry.Value, isBold: false, isItalic: false);
            LoadAndStoreStandardFont(entry.Key, entry.Value, isBold: true, isItalic: false);
            LoadAndStoreStandardFont(entry.Key, entry.Value, isBold: false, isItalic: true);
            LoadAndStoreStandardFont(entry.Key, entry.Value, isBold: true, isItalic: true);
        }

        _fallback = LoadTypeface([fallbackFontName], isBold: false, isItalic: false)
            ?? throw new InvalidOperationException($"Could not load fallback font '{fallbackFontName}'.");
    }

    /// <inheritdoc/>
    public IPdfTypeface GetTypeface(in PdfSubstitutionInfo substitutionInfo, string? unicode, float? width)
    {
        PdfStandardFontName? standardName = substitutionInfo.GetStandardName();
        if (standardName.HasValue)
        {
            PdfSubstitutionInfo standardKey = new(standardName.Value, substitutionInfo.IsBold, substitutionInfo.IsItalic);
            if (_standardFonts.TryGetValue(standardKey, out IPdfTypeface? standardTypeface) && standardTypeface.ContainsAllGlyphs(unicode))
            {
                return standardTypeface;
            }
        }

        IPdfTypeface? namedTypeface = GetOrLoadNamedFont(substitutionInfo);
        if (namedTypeface != null && namedTypeface.ContainsAllGlyphs(unicode))
        {
            return namedTypeface;
        }

        return _fallback;
    }

    private void LoadAndStoreStandardFont(PdfStandardFontName standardFontName, string[] familyNameCandidates, bool isBold, bool isItalic)
    {
        IPdfTypeface? typeface = LoadTypeface(familyNameCandidates, isBold, isItalic);
        if (typeface != null)
        {
            _standardFonts[new PdfSubstitutionInfo(standardFontName, isBold, isItalic)] = typeface;
        }
    }

    private IPdfTypeface? GetOrLoadNamedFont(in PdfSubstitutionInfo substitutionInfo)
    {
        if (string.IsNullOrEmpty(substitutionInfo.NormalizedStem))
        {
            return null;
        }

        if (_namedFonts.TryGetValue(substitutionInfo, out IPdfTypeface? cached))
        {
            return cached;
        }

        IPdfTypeface? typeface = LoadTypeface([substitutionInfo.NormalizedStem], substitutionInfo.IsBold, substitutionInfo.IsItalic);
        if (typeface != null)
        {
            _namedFonts[substitutionInfo] = typeface;
        }

        return typeface;
    }

    private IPdfTypeface? LoadTypeface(string[] familyNameCandidates, bool isBold, bool isItalic)
    {
        for (int i = 0; i < familyNameCandidates.Length; i++)
        {
            string? filePath = _fontDirectoryIndex.FindFontFile(familyNameCandidates[i], isBold, isItalic);
            if (filePath != null)
            {
                byte[] fontBytes = File.ReadAllBytes(filePath);
                return PdfTypefaceFactory.Create(fontBytes, PdfTypefaceType.TrueType, _loggerFactory);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _standardFonts.Clear();
        _namedFonts.Clear();
    }
}
