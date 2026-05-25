namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// ICC parametric curve type identifiers per ICC spec (0..4 currently supported).
/// </summary>
public enum IccTrcParametricType
{
    /// <summary>
    /// Not a parametric curve.
    /// </summary>
    None = -1,

    /// <summary>
    /// y = x^g
    /// </summary>
    Gamma = 0,

    /// <summary>
    /// y = (a·x + b)^g for x ? ?b/a; else 0
    /// </summary>
    PowerWithOffset = 1,

    /// <summary>
    /// y = (a·x + b)^g + c for x ? ?b/a; else c
    /// </summary>
    PowerWithOffsetAndC = 2,

    /// <summary>
    /// y = (a·x + b)^g for x ? d; else c·x
    /// </summary>
    PowerWithLinearSegment = 3,

    /// <summary>
    /// y = (a·x + b)^g + e for x ? d; else c·x + f
    /// </summary>
    PowerWithLinearSegmentAndOffset = 4
}
