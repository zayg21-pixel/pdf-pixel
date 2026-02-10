using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents the various PDF annotation subtypes as defined in the PDF specification.
/// </summary>
[PdfEnum]
public enum PdfAnnotationSubType
{
    /// <summary>
    /// Unknown or unsupported annotation subtype.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Text annotation (sticky note).
    /// </summary>
    [PdfEnumValue("Text")]
    Text,

    /// <summary>
    /// Link annotation.
    /// </summary>
    [PdfEnumValue("Link")]
    Link,

    /// <summary>
    /// Free text annotation.
    /// </summary>
    [PdfEnumValue("FreeText")]
    FreeText,

    /// <summary>
    /// Line annotation.
    /// </summary>
    [PdfEnumValue("Line")]
    Line,

    /// <summary>
    /// Square annotation.
    /// </summary>
    [PdfEnumValue("Square")]
    Square,

    /// <summary>
    /// Circle annotation.
    /// </summary>
    [PdfEnumValue("Circle")]
    Circle,

    /// <summary>
    /// Polygon annotation.
    /// </summary>
    [PdfEnumValue("Polygon")]
    Polygon,

    /// <summary>
    /// Polyline annotation.
    /// </summary>
    [PdfEnumValue("PolyLine")]
    PolyLine,

    /// <summary>
    /// Highlight annotation.
    /// </summary>
    [PdfEnumValue("Highlight")]
    Highlight,

    /// <summary>
    /// Underline annotation.
    /// </summary>
    [PdfEnumValue("Underline")]
    Underline,

    /// <summary>
    /// Squiggly underline annotation.
    /// </summary>
    [PdfEnumValue("Squiggly")]
    Squiggly,

    /// <summary>
    /// Strikeout annotation.
    /// </summary>
    [PdfEnumValue("StrikeOut")]
    StrikeOut,

    /// <summary>
    /// Rubber stamp annotation.
    /// </summary>
    [PdfEnumValue("Stamp")]
    Stamp,

    /// <summary>
    /// Caret annotation.
    /// </summary>
    [PdfEnumValue("Caret")]
    Caret,

    /// <summary>
    /// Ink annotation (freehand drawing).
    /// </summary>
    [PdfEnumValue("Ink")]
    Ink,

    /// <summary>
    /// Pop-up annotation.
    /// </summary>
    [PdfEnumValue("Popup")]
    Popup,

    /// <summary>
    /// File attachment annotation.
    /// </summary>
    [PdfEnumValue("FileAttachment")]
    FileAttachment,

    /// <summary>
    /// Sound annotation.
    /// </summary>
    [PdfEnumValue("Sound")]
    Sound,

    /// <summary>
    /// Movie annotation.
    /// </summary>
    [PdfEnumValue("Movie")]
    Movie,

    /// <summary>
    /// Widget annotation (form field).
    /// </summary>
    [PdfEnumValue("Widget")]
    Widget,

    /// <summary>
    /// Screen annotation.
    /// </summary>
    [PdfEnumValue("Screen")]
    Screen,

    /// <summary>
    /// Printer mark annotation.
    /// </summary>
    [PdfEnumValue("PrinterMark")]
    PrinterMark,

    /// <summary>
    /// Trap network annotation.
    /// </summary>
    [PdfEnumValue("TrapNet")]
    TrapNet,

    /// <summary>
    /// Watermark annotation.
    /// </summary>
    [PdfEnumValue("Watermark")]
    Watermark,

    /// <summary>
    /// 3D annotation.
    /// </summary>
    [PdfEnumValue("3D")]
    ThreeD,

    /// <summary>
    /// Redact annotation.
    /// </summary>
    [PdfEnumValue("Redact")]
    Redact
}