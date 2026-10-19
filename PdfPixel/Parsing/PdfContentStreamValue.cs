namespace PdfPixel.Parsing;

/// <summary>
/// A token of a content stream: a number, boolean, null or operator held by value, or an index into
/// the values its <see cref="PdfParseContext"/> read.
/// </summary>
internal readonly struct PdfContentStreamValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfContentStreamValue"/> struct.
    /// </summary>
    /// <param name="kind">Kind of the token.</param>
    /// <param name="value">The token's value, or its index.</param>
    public PdfContentStreamValue(PdfContentStreamValueKind kind, int value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>
    /// Gets the kind of the token.
    /// </summary>
    public PdfContentStreamValueKind Kind { get; }

    /// <summary>
    /// Gets the integer, the bits of the real, 1 or 0 for a boolean, the operator, or the index into
    /// <see cref="PdfParseContext.Values"/>, depending on <see cref="Kind"/>.
    /// </summary>
    public int Value { get; }
}
