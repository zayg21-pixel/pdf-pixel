namespace PdfPixel.Fonts.Cff;

/// <summary>
/// A token of a CFF DICT or charstring: an integer, real or operator held by value.
/// </summary>
internal readonly struct CffValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CffValue"/> struct.
    /// </summary>
    /// <param name="type">Type of the token.</param>
    /// <param name="value">The token's value.</param>
    public CffValue(CffValueType type, int value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>
    /// Gets the type of the token.
    /// </summary>
    public CffValueType Type { get; }

    /// <summary>
    /// Gets the integer, the bits of the real, or the operator, depending on <see cref="Type"/>.
    /// </summary>
    public int Value { get; }
}
