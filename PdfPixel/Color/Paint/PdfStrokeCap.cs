namespace PdfPixel.Color.Paint;

/// <summary>
/// Line cap style (J operator).
/// </summary>
public enum PdfStrokeCap
{
    /// <summary>
    /// No projection beyond the endpoint.
    /// </summary>
    Butt,

    /// <summary>
    /// Semicircular projection beyond the endpoint.
    /// </summary>
    Round,

    /// <summary>
    /// Square projection beyond the endpoint.
    /// </summary>
    Square
}
