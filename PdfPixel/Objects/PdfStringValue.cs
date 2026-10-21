namespace PdfPixel.Objects;

/// <summary>
/// Represents a PDF string object (literal or hexadecimal).
/// </summary>
public sealed class PdfStringValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfStringValue"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    internal PdfStringValue(in PdfString value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public PdfString Value { get; }

    /// <inheritdoc/>
    public override string ToString() => $"({Value})";
}
