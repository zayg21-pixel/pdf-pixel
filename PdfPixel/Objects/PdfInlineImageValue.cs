namespace PdfPixel.Objects;

/// <summary>
/// Represents a fully parsed inline image (BI/ID/EI) embedded within a content stream,
/// carrying its image dictionary and raw stream bytes as a <see cref="PdfObject"/>.
/// </summary>
public sealed class PdfInlineImageValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfInlineImageValue"/> class.
    /// </summary>
    /// <param name="value">The inline image object.</param>
    internal PdfInlineImageValue(PdfObject value) => Value = value;

    /// <summary>
    /// Gets the inline image object.
    /// </summary>
    public PdfObject Value { get; }

    /// <inheritdoc/>
    public override string ToString() => "[inline image]";
}
