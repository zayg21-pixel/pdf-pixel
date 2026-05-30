using PdfPixel.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Encoding for single byte fonts (Type1, TrueType, etc...)
/// </summary>
[PdfEnum]
public enum PdfFontEncoding
{
    /// <summary>
    /// The encoding is not specified or was not recognised.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Adobe Standard Encoding (PDF spec name: StandardEncoding).
    /// </summary>
    [PdfEnumValue("StandardEncoding")]
    StandardEncoding,

    /// <summary>
    /// Mac OS Roman encoding (PDF spec name: MacRomanEncoding).
    /// </summary>
    [PdfEnumValue("MacRomanEncoding")]
    MacRomanEncoding,

    /// <summary>
    /// Windows ANSI encoding, equivalent to Windows code page 1252 (PDF spec name: WinAnsiEncoding).
    /// </summary>
    [PdfEnumValue("WinAnsiEncoding")]
    WinAnsiEncoding,

    /// <summary>
    /// Mac OS Expert encoding for expert-set typefaces (PDF spec name: MacExpertEncoding).
    /// </summary>
    [PdfEnumValue("MacExpertEncoding")]
    MacExpertEncoding,

    /// <summary>
    /// Adobe Symbol encoding used by Symbol fonts (PDF spec name: SymbolEncoding).
    /// </summary>
    [PdfEnumValue("SymbolEncoding")]
    SymbolEncoding,

    /// <summary>
    /// ITC Zapf Dingbats encoding used by ZapfDingbats fonts (PDF spec name: ZapfDingbatsEncoding).
    /// </summary>
    [PdfEnumValue("ZapfDingbatsEncoding")]
    ZapfDingbatsEncoding
}
