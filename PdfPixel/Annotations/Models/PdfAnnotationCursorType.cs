namespace PdfPixel.Annotations.Models;

/// <summary>
/// Cursor types for annotations.
/// </summary>
public enum PdfAnnotationCursorType
{
    /// <summary>
    /// Default arrow cursor.
    /// </summary>
    Arrow,

    /// <summary>
    /// Hand cursor for clickable widgets/annotations.
    /// </summary>
    Hand,

    /// <summary>
    /// Text input cursor (I-beam) for text fields.
    /// </summary>
    IBeam
}
