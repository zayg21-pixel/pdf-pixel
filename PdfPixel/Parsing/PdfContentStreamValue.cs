namespace PdfPixel.Parsing;

/// <summary>
/// A token of a content stream: a number, boolean, null or operator held by value, or the extent of a name, string, array,
/// or dictionary in the data of its <see cref="PdfParseContext"/>, parsed only when an operator asks for it.
/// </summary>
internal readonly struct PdfContentStreamValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfContentStreamValue"/> struct.
    /// </summary>
    /// <param name="kind">Kind of the token.</param>
    /// <param name="value">The token's value, or the start of its extent.</param>
    /// <param name="length">The length of the token's extent.</param>
    public PdfContentStreamValue(PdfContentStreamValueKind kind, int value, int length = 0)
    {
        Kind = kind;
        Value = value;
        Length = length;
    }

    /// <summary>
    /// Gets the kind of the token.
    /// </summary>
    public PdfContentStreamValueKind Kind { get; }

    /// <summary>
    /// Gets the integer, the bits of the real, 1 or 0 for a boolean, the operator, or the start of the token's extent
    /// in the data of its <see cref="PdfParseContext"/>, depending on <see cref="Kind"/>.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Gets the length of the token's extent, for <see cref="PdfContentStreamValueKind.Value"/>.
    /// </summary>
    public int Length { get; }
}
