namespace PdfPixel.Color.Paint;

/// <summary>
/// Whether a <see cref="PdfPaint"/> is used for filling, stroking or clipping.
/// </summary>
public enum PdfPaintStyle
{
    /// <summary>
    /// Paint used for fill operations.
    /// </summary>
    Fill,

    /// <summary>
    /// Paint used for stroke operations.
    /// </summary>
    Stroke,

    /// <summary>
    /// Paint used to intersect the clip with the area a fill would paint; it paints nothing.
    /// </summary>
    Clip
}
