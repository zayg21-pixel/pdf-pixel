namespace PdfPixel.Color.Paint;

/// <summary>
/// Enumeration for different path/text painting operations.
/// </summary>
public enum PdfPaintOperation
{
    /// <summary>
    /// Paint along the path's outline only.
    /// </summary>
    Stroke,

    /// <summary>
    /// Fill the path's interior only.
    /// </summary>
    Fill,

    /// <summary>
    /// Fill the interior and paint the outline.
    /// </summary>
    FillAndStroke
}
