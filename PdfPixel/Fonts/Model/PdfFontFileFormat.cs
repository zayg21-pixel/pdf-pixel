using PdfPixel.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Font file format enumeration
/// Determined by embedded font stream and its dictionary (/FontFile, /FontFile2, /FontFile3 + /Subtype)
/// </summary>
[PdfEnum]
public enum PdfFontFileFormat
{
    /// <summary>
    /// Unknown or unspecified font file format.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Type 1 font program (PFA/PFB) (/Type1).
    /// </summary>
    [PdfEnumValue("Type1")]
    Type1,

    /// <summary>
    /// TrueType font program (/TrueType).
    /// </summary>
    [PdfEnumValue("TrueType")]
    TrueType,

    /// <summary>
    /// Compact Font Format (CFF) Type 1 font program (/Type1C).
    /// </summary>
    [PdfEnumValue("Type1C")]
    Type1C,

    /// <summary>
    /// CFF for CIDFonts (/CIDFontType0C).
    /// </summary>
    [PdfEnumValue("CIDFontType0C")]
    CIDFontType0C
}
