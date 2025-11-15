using PdfReader.Rendering.State;

namespace PdfReader.Rendering.Operators;

/// <summary>
/// Defines the contract for PDF operator processor components.
/// Implementations encapsulate logic for a specific subset of PDF content stream operators.
/// </summary>
public interface IOperatorProcessor
{
    /// <summary>
    /// Determines whether this processor can handle the specified operator token.
    /// </summary>
    /// <param name="op">The operator token (e.g. "q", "BT", "Tj").</param>
    /// <returns>True if this processor supports the operator; otherwise false.</returns>
    bool CanProcess(string op);

    /// <summary>
    /// Processes an operator assuming <see cref="CanProcess"/> returned true.
    /// Implementations must not throw for unknown operators; caller ensures eligibility.
    /// </summary>
    /// <param name="op">The operator token.</param>
    /// <param name="graphicsState">The current graphics state (may be mutated or replaced).</param>
    void ProcessOperator(string op, ref PdfGraphicsState graphicsState);
}
