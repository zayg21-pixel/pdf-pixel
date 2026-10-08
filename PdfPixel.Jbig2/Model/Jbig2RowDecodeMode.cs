namespace PdfPixel.Jbig2.Model;

/// <summary>
/// How the rows of a <see cref="Jbig2RowTemplate"/> are decoded.
/// </summary>
internal enum Jbig2RowDecodeMode
{
    /// <summary>
    /// Rows are decoded from the template row groups.
    /// </summary>
    Template,

    /// <summary>
    /// Rows are decoded from the template row groups pixel by pixel. Used when a current-row pixel lies
    /// further left than the 64-bit rolling buffer of <see cref="Template"/> holds, notably pattern
    /// dictionaries with AT[0] = (-PW, 0) for large PW. Threshold is conservative: real cap is -63, -32 keeps headroom.
    /// </summary>
    SlowTemplate,

    /// <summary>
    /// Generic template 0 with the default AT pixels (3, -1), (-3, -1), (2, -2) and (-2, -2).
    /// </summary>
    DefaultTemplate0,

    /// <summary>
    /// Refinement template 0 with the default AT pixels (-1, -1) and (-1, -1).
    /// </summary>
    DefaultRefinementTemplate0
}
