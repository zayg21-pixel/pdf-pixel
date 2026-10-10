using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Type of a footnote or endnote element (NoteType, Well-Tagged PDF).
/// </summary>
[PdfEnum]
public enum PdfStructureNoteType
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Footnote.
    /// </summary>
    [PdfEnumValue("Footnote")]
    Footnote,

    /// <summary>
    /// Endnote.
    /// </summary>
    [PdfEnumValue("Endnote")]
    Endnote,

    /// <summary>
    /// Type not specified.
    /// </summary>
    [PdfEnumValue("None")]
    None
}
