using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// Tab order used for annotations on a page (page /Tabs).
/// </summary>
[PdfEnum]
public enum PdfTabOrder
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown = 0,

    /// <summary>
    /// Row order (<c>R</c>).
    /// </summary>
    [PdfEnumValue("R")]
    Row,

    /// <summary>
    /// Column order (<c>C</c>).
    /// </summary>
    [PdfEnumValue("C")]
    Column,

    /// <summary>
    /// Structure order (<c>S</c>).
    /// </summary>
    [PdfEnumValue("S")]
    Structure,

    /// <summary>
    /// Order of the page's /Annots array (<c>A</c>, PDF 2.0).
    /// </summary>
    [PdfEnumValue("A")]
    AnnotationsArray,

    /// <summary>
    /// /Annots array order, widget annotations first (<c>W</c>, PDF 2.0).
    /// </summary>
    [PdfEnumValue("W")]
    Widget
}
