using PdfRender.Fonts.Mapping;
using PdfRender.Fonts.Model;
using PdfRender.Models;
using PdfRender.Resources;
using System.Collections.Generic;

namespace PdfRender.Text;

/// <summary>
/// Utility for converting single-byte PDF codes with base encodings
/// to standard glyph names.
/// </summary>
internal static class SingleByteEncodings
{
    /// <summary>
    /// The PDF string representing an undefined character (".notdef").
    /// </summary>
    public static readonly PdfString UndefinedCharacter = (PdfString)".notdef"u8;

    private static readonly PdfString[] standard;
    private static readonly PdfString[] ansi;
    private static readonly PdfString[] macRoman;
    private static readonly PdfString[] macExpert;
    private static readonly PdfString[] symbol;
    private static readonly PdfString[] zapfDingbats;

    // Predefined Standard 14 font name mappings to encodings
    private static readonly (PdfStandardFontName Name, PdfFontEncoding Encoding)[] Standard14NameEncodings =
    [
        (PdfStandardFontName.Times, PdfFontEncoding.StandardEncoding),
        (PdfStandardFontName.TimesNewRoman, PdfFontEncoding.StandardEncoding),
        (PdfStandardFontName.TimesNewRomanPS, PdfFontEncoding.StandardEncoding),
        (PdfStandardFontName.Helvetica, PdfFontEncoding.StandardEncoding),
        (PdfStandardFontName.Arial, PdfFontEncoding.StandardEncoding),
        (PdfStandardFontName.Courier, PdfFontEncoding.StandardEncoding),
        (PdfStandardFontName.CourierNew, PdfFontEncoding.StandardEncoding),
        (PdfStandardFontName.CourierNewPS, PdfFontEncoding.StandardEncoding),
        (PdfStandardFontName.Symbol, PdfFontEncoding.SymbolEncoding),
        (PdfStandardFontName.ZapfDingbats, PdfFontEncoding.ZapfDingbatsEncoding),
    ];

    static SingleByteEncodings()
    {
        var standardData = PdfResourceLoader.GetResource("StandardEncodings.bin");
        standard = PdfTextResourceConverter.FromPdfStringBlob(standardData);

        var ansiData = PdfResourceLoader.GetResource("AnsiEncodings.bin");
        ansi = PdfTextResourceConverter.FromPdfStringBlob(ansiData);

        var macRomanData = PdfResourceLoader.GetResource("MacRomanEncodings.bin");
        macRoman = PdfTextResourceConverter.FromPdfStringBlob(macRomanData);

        var macExpertData = PdfResourceLoader.GetResource("MacExpertEncodings.bin");
        macExpert = PdfTextResourceConverter.FromPdfStringBlob(macExpertData);

        var symbolData = PdfResourceLoader.GetResource("SymbolEncodings.bin");
        symbol = PdfTextResourceConverter.FromPdfStringBlob(symbolData);

        var zapfDingbatsData = PdfResourceLoader.GetResource("ZapfDingbatsEncodings.bin");
        zapfDingbats = PdfTextResourceConverter.FromPdfStringBlob(zapfDingbatsData);
    }

    /// <summary>
    /// Gets the encoding set (array of glyph names) for the specified PDF font encoding.
    /// </summary>
    /// <param name="encoding">The PDF font encoding to retrieve.</param>
    /// <returns>An array of <see cref="PdfString"/> representing the glyph names for the encoding, or null if unknown.</returns>
    public static PdfString[] GetEncodingSet(PdfFontEncoding encoding)
    {
        return encoding switch
        {
            PdfFontEncoding.StandardEncoding => standard,
            PdfFontEncoding.WinAnsiEncoding => ansi,
            PdfFontEncoding.MacExpertEncoding => macExpert,
            PdfFontEncoding.MacRomanEncoding => macRoman,
            PdfFontEncoding.SymbolEncoding => symbol,
            PdfFontEncoding.ZapfDingbatsEncoding => zapfDingbats,
            _ => default,
        };
    }

    /// <summary>
    /// Gets the glyph name for a given code and encoding, optionally applying differences.
    /// </summary>
    /// <param name="code">The single-byte code to look up.</param>
    /// <param name="encoding">The PDF font encoding to use.</param>
    /// <param name="differences">Optional dictionary of code-to-name overrides (PDF Differences array).</param>
    /// <returns>The <see cref="PdfString"/> glyph name for the code, or <see cref="PdfString.Empty"/> if not found.</returns>
    public static PdfString GetNameByCode(byte code, PdfFontEncoding encoding, Dictionary<int, PdfString> differences = default)
    {
        if (differences != null && differences.TryGetValue(code, out PdfString name) && !name.IsEmpty)
        {
            return name;
        }

        return encoding switch
        {
            PdfFontEncoding.StandardEncoding => standard[code],
            PdfFontEncoding.MacRomanEncoding => macRoman[code],
            PdfFontEncoding.WinAnsiEncoding => ansi[code],
            PdfFontEncoding.MacExpertEncoding => macExpert[code],
            PdfFontEncoding.SymbolEncoding => symbol[code],
            PdfFontEncoding.ZapfDingbatsEncoding => zapfDingbats[code],
            _ => default,
        };
    }

    /// <summary>
    /// Gets the glyph name for a given code and encoding, or <see cref="UndefinedCharacter"/> if not found.
    /// </summary>
    /// <param name="code">The single-byte code to look up.</param>
    /// <param name="encoding">The PDF font encoding to use.</param>
    /// <param name="differences">Optional dictionary of code-to-name overrides (PDF Differences array).</param>
    /// <returns>The <see cref="PdfString"/> glyph name for the code, or <see cref="UndefinedCharacter"/> if not found or empty.</returns>
    public static PdfString GetNameByCodeOrUndefined(byte code, PdfFontEncoding encoding, Dictionary<int, PdfString> differences = default)
    {
        var result = GetNameByCode(code, encoding, differences);

        if (result.IsEmpty)
        {
            return UndefinedCharacter;
        }
        else
        {
            return result;
        }
    }

    /// <summary>
    /// Detects the expected encoding by Standard 14 font name.
    /// Returns null for non-standard fonts.
    /// </summary>
    /// <param name="fontName">The PDF font name (e.g., BaseFont or FontName).</param>
    /// <returns>The corresponding <see cref="PdfFontEncoding"/>, or null if no Standard 14 match is found.</returns>
    public static PdfFontEncoding? GetEncodingByName(PdfString fontName)
    {
        if (fontName.IsEmpty)
        {
            return default;
        }

        var name = fontName.ToString();
        if (string.IsNullOrEmpty(name))
        {
            return default;
        }

        for (int i = 0; i < Standard14NameEncodings.Length; i++)
        {
            var pair = Standard14NameEncodings[i];
            if (name.IndexOf(pair.Name.ToString(), System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return pair.Encoding;
            }
        }

        return default;
    }
}
