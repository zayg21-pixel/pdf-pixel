namespace PdfPixel.Models;

/// <summary>
/// Represents a content stream operator.
/// </summary>
public sealed class PdfOperatorValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOperatorValue"/> class.
    /// </summary>
    /// <param name="value">The operator token.</param>
    internal PdfOperatorValue(in PdfString value) => Value = value;

    /// <summary>
    /// Gets the operator token.
    /// </summary>
    public PdfString Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
