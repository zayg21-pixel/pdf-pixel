using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Fonts.Management;

// TODO: [HIGH] InMemoryFontSubstitutor must register fonts with style, otherwise we can't find a match

/// <summary>
/// Loads typefaces from explicitly registered in-memory font data, for environments where no system
/// fonts are available, such as browser/WASM. Font programs are registered once as raw bytes; each
/// resolution parses a new typeface over those same bytes, so the caller owns and disposes what it
/// receives without ever affecting the registration.
/// </summary>
public sealed class InMemoryFontSubstitutor : IFontSubstitutor
{
    private static readonly SfntPdfTypefaceParameters SubstitutedTypefaceParameters = new() { RepackTypeface = false };

    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, byte[]> _namedFontData = new(StringComparer.OrdinalIgnoreCase);
    private byte[]? _fallbackFontData;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryFontSubstitutor"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during font parsing.</param>
    public InMemoryFontSubstitutor(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    /// <summary>
    /// Registers font data to use as the last-resort typeface when no registered font matches a
    /// requested name or glyph.
    /// </summary>
    /// <param name="fontData">Raw SFNT font program bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fontData"/> is not a valid SFNT font program.</exception>
    public void RegisterFallback(in ReadOnlyMemory<byte> fontData)
    {
        byte[] data = fontData.ToArray();
        ValidateFontData(data);
        _fallbackFontData = data;
    }

    /// <summary>
    /// Registers font data for a standard PDF font name. The data is registered under the names PDF
    /// documents use for that font, so that it resolves by family name as well.
    /// </summary>
    /// <param name="standardFont">The standard PDF font to associate with the supplied font data.</param>
    /// <param name="fontData">Raw SFNT font program bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fontData"/> is not a valid SFNT font program.</exception>
    public void RegisterStandardFont(PdfStandardFontName standardFont, in ReadOnlyMemory<byte> fontData)
    {
        byte[] data = fontData.ToArray();
        ValidateFontData(data);

        _namedFontData[standardFont.ToString()] = data;

        if (FontSubstitutionMaps.CanonicalNames.Candidates.TryGetValue(standardFont, out IReadOnlyList<string>? displayNames))
        {
            foreach (string displayName in displayNames)
            {
                _namedFontData[displayName] = data;
            }
        }
    }

    /// <summary>
    /// Registers font data under an explicit family name.
    /// </summary>
    /// <param name="name">The family name PDF documents use to reference this font.</param>
    /// <param name="fontData">Raw SFNT font program bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fontData"/> is not a valid SFNT font program.</exception>
    public void RegisterFont(string name, in ReadOnlyMemory<byte> fontData)
    {
        byte[] data = fontData.ToArray();
        ValidateFontData(data);
        _namedFontData[name] = data;
    }

    /// <inheritdoc />
    public SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo)
    {
        if (!_namedFontData.TryGetValue(substitutionInfo.NormalizedStem, out byte[]? data))
        {
            return null;
        }

        return CreateTypeface(data);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Every registered font is parsed in turn until one covers <paramref name="unicode"/>; the ones
    /// that do not are disposed again before the next is tried.
    /// </remarks>
    public SfntPdfTypeface? ResolveByCharacter(in PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        foreach (byte[] data in _namedFontData.Values)
        {
            SfntPdfTypeface typeface = CreateTypeface(data);
            if (typeface.ContainsAllGlyphs(unicode))
            {
                return typeface;
            }

            typeface.Dispose();
        }

        return null;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when no fallback font has been registered.</exception>
    public SfntPdfTypeface GetFallbackTypeface()
    {
        if (_fallbackFontData == null)
        {
            throw new InvalidOperationException("No fallback font has been registered.");
        }

        return CreateTypeface(_fallbackFontData);
    }

    /// <summary>
    /// Parses <paramref name="fontData"/> and discards the result, so that invalid font data is
    /// reported as an error at registration rather than at the first resolution.
    /// </summary>
    private void ValidateFontData(byte[] fontData)
    {
        SfntPdfTypeface typeface = CreateTypeface(fontData);
        typeface.Dispose();
    }

    private SfntPdfTypeface CreateTypeface(byte[] fontData)
        => new(new MemoryStream(fontData, writable: false), _loggerFactory, SubstitutedTypefaceParameters);
}
