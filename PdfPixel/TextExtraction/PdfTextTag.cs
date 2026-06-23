using PdfPixel.Text;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Standard structure tags relevant for text extraction.
/// </summary>
[PdfEnum]
public enum PdfTextTag
{
    /// <summary>
    /// Tag not recognized as a standard structure tag.
    /// The raw tag name is available in <see cref="PdfTextMarkup.CustomTag"/>.
    /// </summary>
    [PdfEnumDefaultValue]
    Custom,

    /// <summary>
    /// Non-logical content (headers, footers, page numbers, watermarks).
    /// Should be excluded from text selection and extraction.
    /// </summary>
    [PdfEnumValue("Artifact")]
    Artifact,

    /// <summary>
    /// Inline text span.
    /// </summary>
    [PdfEnumValue("Span")]
    Span,

    /// <summary>
    /// Paragraph.
    /// </summary>
    [PdfEnumValue("P")]
    Paragraph,

    /// <summary>
    /// Graphical content.
    /// </summary>
    [PdfEnumValue("Figure")]
    Figure,

    /// <summary>
    /// Heading level 1.
    /// </summary>
    [PdfEnumValue("H1")]
    H1,

    /// <summary>
    /// Heading level 2.
    /// </summary>
    [PdfEnumValue("H2")]
    H2,

    /// <summary>
    /// Heading level 3.
    /// </summary>
    [PdfEnumValue("H3")]
    H3,

    /// <summary>
    /// Heading level 4.
    /// </summary>
    [PdfEnumValue("H4")]
    H4,

    /// <summary>
    /// Heading level 5.
    /// </summary>
    [PdfEnumValue("H5")]
    H5,

    /// <summary>
    /// Heading level 6.
    /// </summary>
    [PdfEnumValue("H6")]
    H6,

    /// <summary>
    /// Table.
    /// </summary>
    [PdfEnumValue("Table")]
    Table,

    /// <summary>
    /// Table row.
    /// </summary>
    [PdfEnumValue("TR")]
    TableRow,

    /// <summary>
    /// Table header cell.
    /// </summary>
    [PdfEnumValue("TH")]
    TableHeader,

    /// <summary>
    /// Table data cell.
    /// </summary>
    [PdfEnumValue("TD")]
    TableData,

    /// <summary>
    /// List.
    /// </summary>
    [PdfEnumValue("L")]
    List,

    /// <summary>
    /// List item.
    /// </summary>
    [PdfEnumValue("LI")]
    ListItem
}
