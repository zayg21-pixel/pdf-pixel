using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Stroke pattern of a border edge (BorderStyle, TBorderStyle).
/// </summary>
[PdfEnum]
public enum PdfStructureBorderStyle
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// No border.
    /// </summary>
    [PdfEnumValue("None")]
    None,

    /// <summary>
    /// No border, except for table border conflict resolution.
    /// </summary>
    [PdfEnumValue("Hidden")]
    Hidden,

    /// <summary>
    /// A series of dots.
    /// </summary>
    [PdfEnumValue("Dotted")]
    Dotted,

    /// <summary>
    /// A series of short line segments.
    /// </summary>
    [PdfEnumValue("Dashed")]
    Dashed,

    /// <summary>
    /// A single line segment.
    /// </summary>
    [PdfEnumValue("Solid")]
    Solid,

    /// <summary>
    /// Two solid lines.
    /// </summary>
    [PdfEnumValue("Double")]
    Double,

    /// <summary>
    /// Carved into the canvas.
    /// </summary>
    [PdfEnumValue("Groove")]
    Groove,

    /// <summary>
    /// Coming out of the canvas.
    /// </summary>
    [PdfEnumValue("Ridge")]
    Ridge,

    /// <summary>
    /// The box looks embedded in the canvas.
    /// </summary>
    [PdfEnumValue("Inset")]
    Inset,

    /// <summary>
    /// The box looks coming out of the canvas.
    /// </summary>
    [PdfEnumValue("Outset")]
    Outset
}
