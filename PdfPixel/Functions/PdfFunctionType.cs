namespace PdfPixel.Functions;

/// <summary>
/// Enumerates supported PDF function types.
/// </summary>
public enum PdfFunctionType
{
    /// <summary>
    /// Unrecognized or unsupported function type.
    /// </summary>
    Unknown = -1,

    /// <summary>
    /// Type 0: sampled function that uses interpolation over a table of values.
    /// </summary>
    Sampled = 0,

    /// <summary>
    /// Type 2: exponential interpolation function defined by a power-law expression.
    /// </summary>
    Exponential = 2,

    /// <summary>
    /// Type 3: stitching function that combines multiple sub-functions over partitioned domains.
    /// </summary>
    Stitching = 3,

    /// <summary>
    /// Type 4: PostScript calculator function expressed as a subset of PostScript operators.
    /// </summary>
    PostScript = 4
}
