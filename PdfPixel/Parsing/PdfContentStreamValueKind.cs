namespace PdfPixel.Parsing;

/// <summary>
/// Kind of a content stream token.
/// </summary>
internal enum PdfContentStreamValueKind : byte
{
    /// <summary>
    /// An integer.
    /// </summary>
    Integer,

    /// <summary>
    /// A real number.
    /// </summary>
    Real,

    /// <summary>
    /// The keyword true or false.
    /// </summary>
    Boolean,

    /// <summary>
    /// The keyword null.
    /// </summary>
    Null,

    /// <summary>
    /// An operator.
    /// </summary>
    Operator,

    /// <summary>
    /// A name, string, array, dictionary or inline image, held in <see cref="PdfParseContext.Values"/>.
    /// </summary>
    Value
}
