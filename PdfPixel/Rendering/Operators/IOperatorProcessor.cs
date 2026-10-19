using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Defines the contract for PDF operator processor components.
/// Implementations encapsulate logic for a specific subset of PDF content stream operators.
/// </summary>
internal interface IOperatorProcessor
{
    /// <summary>
    /// Processes an operator when it belongs to this processor's subset.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="context">The content stream the operator and its operands were read from.</param>
    /// <param name="graphicsState">The current graphics state (may be mutated or replaced).</param>
    /// <returns>True if this processor processed the operator; otherwise false.</returns>
    bool ProcessOperator(PdfOperator op, PdfParseContext context, ref PdfGraphicsState graphicsState);
}
