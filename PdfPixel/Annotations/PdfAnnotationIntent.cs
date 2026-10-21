using PdfPixel.Objects;

namespace PdfPixel.Annotations;

/// <summary>
/// Intent (/IT) of a markup annotation, distinguishing different uses of a single annotation type.
/// </summary>
[PdfEnum]
public enum PdfAnnotationIntent
{
    /// <summary>
    /// No explicit intent: the entry is absent or not one of the defined values.
    /// </summary>
    [PdfEnumDefaultValue]
    None,

    /// <summary>
    /// Free text annotation functioning as a plain text box comment.
    /// </summary>
    [PdfEnumValue("FreeText")]
    FreeText,

    /// <summary>
    /// Free text annotation functioning as a callout, associated with an area on the page through its callout line.
    /// </summary>
    [PdfEnumValue("FreeTextCallout")]
    FreeTextCallout,

    /// <summary>
    /// Free text annotation functioning as a click-to-type or typewriter object, drawn without a callout line.
    /// </summary>
    [PdfEnumValue("FreeTextTypeWriter")]
    FreeTextTypeWriter,

    /// <summary>
    /// Line annotation functioning as an arrow.
    /// </summary>
    [PdfEnumValue("LineArrow")]
    LineArrow,

    /// <summary>
    /// Line annotation functioning as a dimension line.
    /// </summary>
    [PdfEnumValue("LineDimension")]
    LineDimension,

    /// <summary>
    /// Polygon annotation functioning as a cloud object.
    /// </summary>
    [PdfEnumValue("PolygonCloud")]
    PolygonCloud,

    /// <summary>
    /// Polyline annotation functioning as a dimension.
    /// </summary>
    [PdfEnumValue("PolyLineDimension")]
    PolyLineDimension,

    /// <summary>
    /// Polygon annotation functioning as a dimension.
    /// </summary>
    [PdfEnumValue("PolygonDimension")]
    PolygonDimension,

    /// <summary>
    /// Stamp annotation whose appearance is a rubber stamp.
    /// </summary>
    [PdfEnumValue("Stamp")]
    Stamp,

    /// <summary>
    /// Stamp annotation whose appearance is an image.
    /// </summary>
    [PdfEnumValue("StampImage")]
    StampImage,

    /// <summary>
    /// Stamp annotation whose appearance has been taken from preexisting page content.
    /// </summary>
    [PdfEnumValue("StampSnapshot")]
    StampSnapshot
}
