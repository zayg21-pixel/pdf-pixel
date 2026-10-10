using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// Page layout used when the document is opened (catalog /PageLayout).
/// </summary>
[PdfEnum]
public enum PdfPageLayout
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown = 0,

    /// <summary>
    /// Display one page at a time.
    /// </summary>
    [PdfEnumValue("SinglePage")]
    SinglePage,

    /// <summary>
    /// Display the pages in one column.
    /// </summary>
    [PdfEnumValue("OneColumn")]
    OneColumn,

    /// <summary>
    /// Display the pages in two columns, with odd-numbered pages on the left.
    /// </summary>
    [PdfEnumValue("TwoColumnLeft")]
    TwoColumnLeft,

    /// <summary>
    /// Display the pages in two columns, with odd-numbered pages on the right.
    /// </summary>
    [PdfEnumValue("TwoColumnRight")]
    TwoColumnRight,

    /// <summary>
    /// Display the pages two at a time, with odd-numbered pages on the left.
    /// </summary>
    [PdfEnumValue("TwoPageLeft")]
    TwoPageLeft,

    /// <summary>
    /// Display the pages two at a time, with odd-numbered pages on the right.
    /// </summary>
    [PdfEnumValue("TwoPageRight")]
    TwoPageRight
}
