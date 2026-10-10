using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Kind of content controlled by an optional content group when printing (/Print /Subtype).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentPrintSubtype
{
    /// <summary>
    /// Subtype not defined by the specification; see <see cref="PdfOptionalContentUsage.RawPrintSubtype"/>.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Trapping content.
    /// </summary>
    [PdfEnumValue("Trapping")]
    Trapping,

    /// <summary>
    /// Printer's marks.
    /// </summary>
    [PdfEnumValue("PrintersMarks")]
    PrintersMarks,

    /// <summary>
    /// Watermark.
    /// </summary>
    [PdfEnumValue("Watermark")]
    Watermark
}
