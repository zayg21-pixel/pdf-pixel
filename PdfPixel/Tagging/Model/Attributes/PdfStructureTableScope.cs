using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Cells a table header applies to (Scope).
/// </summary>
[PdfEnum]
public enum PdfStructureTableScope
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Header of its row.
    /// </summary>
    [PdfEnumValue("Row")]
    Row,

    /// <summary>
    /// Header of its column.
    /// </summary>
    [PdfEnumValue("Column")]
    Column,

    /// <summary>
    /// Header of both its row and its column.
    /// </summary>
    [PdfEnumValue("Both")]
    Both
}
