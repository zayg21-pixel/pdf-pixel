namespace PdfPixel.Models;

/// <summary>
/// Distinguishes the two node kinds of a <see cref="PdfVisibilityExpression"/> tree.
/// </summary>
public enum PdfVisibilityExpressionType
{
    /// <summary>
    /// A leaf node referencing a single optional content group.
    /// </summary>
    Group,

    /// <summary>
    /// An internal node combining operands with a boolean operator.
    /// </summary>
    Operator
}
