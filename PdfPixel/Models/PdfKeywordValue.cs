namespace PdfPixel.Models;

/// <summary>
/// Represents a file structure keyword, such as <c>obj</c>, <c>stream</c> or <c>trailer</c>.
/// </summary>
public sealed class PdfKeywordValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfKeywordValue"/> class.
    /// </summary>
    /// <param name="value">The keyword token.</param>
    internal PdfKeywordValue(in PdfString value) => Value = value;

    /// <summary>
    /// Gets the keyword token.
    /// </summary>
    public PdfString Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
