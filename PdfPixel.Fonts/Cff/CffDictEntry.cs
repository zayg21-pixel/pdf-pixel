namespace PdfPixel.Fonts.Cff;

/// <summary>
/// A single CFF DICT entry (an operator together with its preceding operands), preserved verbatim
/// for operators whose value is not individually modeled and must be written back unchanged.
/// </summary>
public class CffDictEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CffDictEntry"/> class.
    /// </summary>
    /// <param name="operator">The operator token. Its <see cref="CffValueType"/> distinguishes plain vs. escaped.</param>
    /// <param name="operands">The operand values preceding the operator, in DICT order.</param>
    public CffDictEntry(ICffValue @operator, ICffValue[] operands)
    {
        Operator = @operator;
        Operands = operands;
    }

    /// <summary>
    /// Gets the operator token.
    /// </summary>
    public ICffValue Operator { get; }

    /// <summary>
    /// Gets the operand values preceding the operator, in DICT order.
    /// </summary>
    public ICffValue[] Operands { get; }
}
