using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// Boolean operator combining the operands of a <see cref="PdfVisibilityExpression"/> node.
/// </summary>
[PdfEnum]
public enum PdfVisibilityExpressionOperator
{
    /// <summary>
    /// Unrecognized or missing operator name.
    /// </summary>
    [PdfEnumDefaultValue]
    Undefined,

    /// <summary>
    /// True only when every operand is true.
    /// </summary>
    [PdfEnumValue("And")]
    And,

    /// <summary>
    /// True when at least one operand is true.
    /// </summary>
    [PdfEnumValue("Or")]
    Or,

    /// <summary>
    /// True when its single operand is false.
    /// </summary>
    [PdfEnumValue("Not")]
    Not
}
