using PdfPixel.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Identifies the subtype of a PDF font dictionary, as specified by the /Subtype entry (PDF spec Table 110 and §9.6).
/// </summary>
[PdfEnum]
public enum PdfFontSubType
{
    /// <summary>
    /// The font subtype is not specified or was not recognised.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Type 0 (Composite) font: supports multi-byte character codes and delegates to descendant CID fonts.
    /// </summary>
    [PdfEnumValue("Type0")]
    Type0,

    /// <summary>
    /// Type 1 font: a PostScript Type 1 font (including embedded CFF/Type1C data).
    /// </summary>
    [PdfEnumValue("Type1")]
    Type1,

    /// <summary>
    /// Multiple Master Type 1 font: a Type 1 font with multiple design axes.
    /// </summary>
    [PdfEnumValue("MMType1")]
    MMType1,

    /// <summary>
    /// Type 3 font: glyphs are defined by PDF content streams within the font dictionary.
    /// </summary>
    [PdfEnumValue("Type3")]
    Type3,

    /// <summary>
    /// TrueType font: a font based on the TrueType outline format.
    /// </summary>
    [PdfEnumValue("TrueType")]
    TrueType,

    /// <summary>
    /// CIDFontType0: a CID-keyed font based on Type 1 (CFF/OTF CFF) outlines. Used as a descendant of a Type 0 font.
    /// </summary>
    [PdfEnumValue("CIDFontType0")]
    CidFontType0,

    /// <summary>
    /// CIDFontType2: a CID-keyed font based on TrueType outlines. Used as a descendant of a Type 0 font.
    /// </summary>
    [PdfEnumValue("CIDFontType2")]
    CidFontType2
}
