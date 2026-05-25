using PdfPixel.Color.Functions;
using System.Numerics;

namespace PdfPixel.Color.Icc.Trc.Vector;

/// <summary>
/// Interface for evaluating ICC TRC curves for multiple channels at once.
/// </summary>
internal interface IIccTrcVectorEvaluator
{
    /// <summary>
    /// Evaluates the TRC for a vector of channel values.
    /// </summary>
    /// <param name="x">Input vector (per channel).</param>
    /// <returns>Evaluated vector (per channel).</returns>
    Vector4 Evaluate(Vector4 x);
}
