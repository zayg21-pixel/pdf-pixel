namespace PdfRender.Color.Icc.Utilities;

/// <summary>
/// Interface for evaluating ICC TRC (tone reproduction curve) values.
/// </summary>
internal interface IIccTrcEvaluator
{
    /// <summary>
    /// Evaluates the curve at the specified normalized input (0..1).
    /// </summary>
    /// <param name="x">Normalized input value (0..1).</param>
    /// <returns>Normalized output value (0..1).</returns>
    float Evaluate(float x);
}
