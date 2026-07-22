namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Specifies the type of value produced while reading a CFF DICT or charstring.
/// </summary>
public enum CffValueType
{
    /// <summary>
    /// Represents an integer operand.
    /// </summary>
    Integer,

    /// <summary>
    /// Represents a real (floating-point) operand.
    /// </summary>
    Real,

    /// <summary>
    /// Represents an operator (DICT operator or charstring command).
    /// </summary>
    Operator,

    /// <summary>
    /// Represents an escaped operator (introduced by the <c>12</c> prefix byte in a DICT).
    /// </summary>
    EscapedOperator
}
