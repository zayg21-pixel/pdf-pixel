namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF name object (identifier starting with '/').
/// </summary>
public sealed class PdfNameValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfNameValue"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    internal PdfNameValue(in PdfString value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public PdfString Value { get; }

    /// <inheritdoc/>
    public override string ToString() => $"/{Value}";
}
