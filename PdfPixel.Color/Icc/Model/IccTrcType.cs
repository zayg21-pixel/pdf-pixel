namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// Represents a tone reproduction curve (TRC) abstraction for an ICC profile channel (Gray or RGB).
/// Encapsulates gamma, sampled, or parametric curve data for color transformations.
/// </summary>
public enum IccTrcType
{
    /// <summary>
    /// Unspecified or unknown TRC kind; treated as identity (linear) by evaluators.
    /// </summary>
    None,

    /// <summary>
    /// Simple gamma exponent curve where y = x^Gamma.
    /// </summary>
    Gamma,

    /// <summary>
    /// Sampled curve with equally spaced samples over input domain [0..1].
    /// </summary>
    Sampled,

    /// <summary>
    /// Parametric curve defined by ICC parametricCurveType and associated parameters.
    /// </summary>
    Parametric
}
