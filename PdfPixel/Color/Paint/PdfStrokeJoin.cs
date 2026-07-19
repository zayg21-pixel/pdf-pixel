namespace PdfPixel.Color.Paint;

/// <summary>
/// Line join style (j operator).
/// </summary>
public enum PdfStrokeJoin
{
    /// <summary>
    /// Sharp corner, extended to the miter limit.
    /// </summary>
    Miter,

    /// <summary>
    /// Rounded corner.
    /// </summary>
    Round,

    /// <summary>
    /// Flattened (beveled) corner.
    /// </summary>
    Bevel
}
