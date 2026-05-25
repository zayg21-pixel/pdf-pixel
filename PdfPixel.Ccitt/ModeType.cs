namespace PdfPixel.Ccitt;

/// <summary>
/// CCITT 2D coding mode as defined in T.4 / T.6.
/// </summary>
public enum ModeType
{
    /// <summary>
    /// Pass mode: skip over the reference line segment between b1 and b2.
    /// </summary>
    Pass,

    /// <summary>
    /// Horizontal mode: two explicit run-length codes follow (a0a1, a1a2).
    /// </summary>
    Horizontal,

    /// <summary>
    /// Vertical mode: a1 is positioned relative to b1 by a signed delta (-3..+3).
    /// </summary>
    Vertical
}
