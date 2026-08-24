using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.Resources;

/// <summary>
/// Utility for converting single-byte codes with base encodings to standard glyph names.
/// </summary>
public static class SingleByteEncodings
{
    /// <summary>
    /// The string representing an undefined character (".notdef").
    /// </summary>
    public static readonly PdfFontString UndefinedCharacter = (PdfFontString)".notdef"u8;

    private static readonly PdfFontString[] _standard;
    private static readonly PdfFontString[] _ansi;
    private static readonly PdfFontString[] _macRoman;
    private static readonly PdfFontString[] _macExpert;
    private static readonly PdfFontString[] _symbol;
    private static readonly PdfFontString[] _zapfDingbats;

    // Predefined Standard 14 font name mappings to encodings
    private static readonly (PdfFontStandardName Name, PdfFontEncoding Encoding)[] Standard14NameEncodings =
    [
        (PdfFontStandardName.Times, PdfFontEncoding.StandardEncoding),
        (PdfFontStandardName.TimesNewRoman, PdfFontEncoding.StandardEncoding),
        (PdfFontStandardName.TimesNewRomanPS, PdfFontEncoding.StandardEncoding),
        (PdfFontStandardName.Helvetica, PdfFontEncoding.StandardEncoding),
        (PdfFontStandardName.Arial, PdfFontEncoding.StandardEncoding),
        (PdfFontStandardName.Courier, PdfFontEncoding.StandardEncoding),
        (PdfFontStandardName.CourierNew, PdfFontEncoding.StandardEncoding),
        (PdfFontStandardName.CourierNewPS, PdfFontEncoding.StandardEncoding),
        (PdfFontStandardName.Symbol, PdfFontEncoding.SymbolEncoding),
        (PdfFontStandardName.ZapfDingbats, PdfFontEncoding.ZapfDingbatsEncoding)
    ];

    static SingleByteEncodings()
    {
        byte[] standardData = FontResourceLoader.GetResource("StandardEncodings.bin");
        _standard = FontResourceConverter.FromPdfStringBlob(standardData);

        byte[] ansiData = FontResourceLoader.GetResource("AnsiEncodings.bin");
        _ansi = FontResourceConverter.FromPdfStringBlob(ansiData);

        byte[] macRomanData = FontResourceLoader.GetResource("MacRomanEncodings.bin");
        _macRoman = FontResourceConverter.FromPdfStringBlob(macRomanData);

        byte[] macExpertData = FontResourceLoader.GetResource("MacExpertEncodings.bin");
        _macExpert = FontResourceConverter.FromPdfStringBlob(macExpertData);

        byte[] symbolData = FontResourceLoader.GetResource("SymbolEncodings.bin");
        _symbol = FontResourceConverter.FromPdfStringBlob(symbolData);

        byte[] zapfDingbatsData = FontResourceLoader.GetResource("ZapfDingbatsEncodings.bin");
        _zapfDingbats = FontResourceConverter.FromPdfStringBlob(zapfDingbatsData);
    }

    /// <summary>
    /// Gets the encoding set (array of glyph names) for the specified font encoding.
    /// </summary>
    /// <param name="encoding">The font encoding to retrieve.</param>
    /// <returns>An array of <see cref="PdfFontString"/> representing the glyph names for the encoding, or null if unknown.</returns>
    public static PdfFontString[]? GetEncodingSet(PdfFontEncoding encoding)
    {
        return encoding switch
        {
            PdfFontEncoding.StandardEncoding => _standard,
            PdfFontEncoding.WinAnsiEncoding => _ansi,
            PdfFontEncoding.MacExpertEncoding => _macExpert,
            PdfFontEncoding.MacRomanEncoding => _macRoman,
            PdfFontEncoding.SymbolEncoding => _symbol,
            PdfFontEncoding.ZapfDingbatsEncoding => _zapfDingbats,
            _ => default
        };
    }

    /// <summary>
    /// Gets the glyph name for a given code and encoding.
    /// </summary>
    /// <param name="code">The single-byte code to look up.</param>
    /// <param name="encoding">The font encoding to use.</param>
    /// <returns>The <see cref="PdfFontString"/> glyph name for the code, or <see cref="PdfFontString.Empty"/> if not found.</returns>
    public static PdfFontString GetNameByCode(byte code, PdfFontEncoding encoding)
    {
        return encoding switch
        {
            PdfFontEncoding.StandardEncoding => _standard[code],
            PdfFontEncoding.MacRomanEncoding => _macRoman[code],
            PdfFontEncoding.WinAnsiEncoding => _ansi[code],
            PdfFontEncoding.MacExpertEncoding => _macExpert[code],
            PdfFontEncoding.SymbolEncoding => _symbol[code],
            PdfFontEncoding.ZapfDingbatsEncoding => _zapfDingbats[code],
            _ => default
        };
    }

    /// <summary>
    /// Gets the glyph name for a given code and encoding, or <see cref="UndefinedCharacter"/> if not found.
    /// </summary>
    /// <param name="code">The single-byte code to look up.</param>
    /// <param name="encoding">The font encoding to use.</param>
    /// <returns>The <see cref="PdfFontString"/> glyph name for the code, or <see cref="UndefinedCharacter"/> if not found or empty.</returns>
    public static PdfFontString GetNameByCodeOrUndefined(byte code, PdfFontEncoding encoding)
    {
        PdfFontString result = GetNameByCode(code, encoding);

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
    /// <param name="fontName">The font name (e.g., BaseFont or FontName).</param>
    /// <returns>The corresponding <see cref="PdfFontEncoding"/>, or null if no Standard 14 match is found.</returns>
    public static PdfFontEncoding? GetEncodingByName(in PdfFontString fontName)
    {
        if (fontName.IsEmpty)
        {
            return default;
        }

        string name = fontName.ToString();
        if (string.IsNullOrEmpty(name))
        {
            return default;
        }

        for (int i = 0; i < Standard14NameEncodings.Length; i++)
        {
            (PdfFontStandardName Name, PdfFontEncoding Encoding) pair = Standard14NameEncodings[i];
            if (name.IndexOf(pair.Name.ToString(), System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return pair.Encoding;
            }
        }

        return default;
    }

    /// <summary>
    /// Gets the default encoding for a Standard 14 font family.
    /// </summary>
    /// <param name="fontName">The Standard 14 font family.</param>
    /// <returns>The corresponding <see cref="PdfFontEncoding"/>, or null if no match is found.</returns>
    public static PdfFontEncoding? GetDefaultEncoding(PdfFontStandardName fontName)
    {
        for (int i = 0; i < Standard14NameEncodings.Length; i++)
        {
            (PdfFontStandardName Name, PdfFontEncoding Encoding) pair = Standard14NameEncodings[i];
            if (pair.Name == fontName)
            {
                return pair.Encoding;
            }
        }

        return default;
    }
}
