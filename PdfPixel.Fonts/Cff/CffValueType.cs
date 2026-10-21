namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Type of a CFF DICT or charstring token.
/// </summary>
internal enum CffValueType : byte
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
