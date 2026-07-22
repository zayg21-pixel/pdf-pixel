namespace PdfPixel.Fonts.Model;

/// <summary>
/// Single-byte glyph-name encoding table used by simple fonts (Type1, TrueType, ...).
/// </summary>
public enum PdfFontEncoding
{
    /// <summary>
    /// The encoding is not specified or was not recognised.
    /// </summary>
    Unknown,

    /// <summary>
    /// Adobe Standard Encoding.
    /// </summary>
    StandardEncoding,

    /// <summary>
    /// Mac OS Roman encoding.
    /// </summary>
    MacRomanEncoding,

    /// <summary>
    /// Windows ANSI encoding, equivalent to Windows code page 1252.
    /// </summary>
    WinAnsiEncoding,

    /// <summary>
    /// Mac OS Expert encoding for expert-set typefaces.
    /// </summary>
    MacExpertEncoding,

    /// <summary>
    /// Adobe Symbol encoding used by Symbol fonts.
    /// </summary>
    SymbolEncoding,

    /// <summary>
    /// ITC Zapf Dingbats encoding used by ZapfDingbats fonts.
    /// </summary>
    ZapfDingbatsEncoding
}
