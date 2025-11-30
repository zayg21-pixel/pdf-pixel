using PdfReader.Text;

namespace PdfReader.Fonts.Model;

/// <summary>
/// Encoding for single byte fonts (Type1, TrueType, etc...)
/// </summary>
[PdfEnum]
public enum PdfFontEncoding
{
    [PdfEnumDefaultValue]
    Unknown,

    [PdfEnumValue("StandardEncoding")]
    StandardEncoding,
    [PdfEnumValue("MacRomanEncoding")]
    MacRomanEncoding,
    [PdfEnumValue("WinAnsiEncoding")]
    WinAnsiEncoding,
    [PdfEnumValue("MacExpertEncoding")]
    MacExpertEncoding
}