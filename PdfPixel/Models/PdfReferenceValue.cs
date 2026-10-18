namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF indirect reference to another object.
/// </summary>
public sealed class PdfReferenceValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfReferenceValue"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    internal PdfReferenceValue(in PdfReference value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public PdfReference Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
