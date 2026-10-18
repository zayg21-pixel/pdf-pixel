namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF dictionary object (key-value pairs).
/// </summary>
public sealed class PdfDictionaryValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDictionaryValue"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    internal PdfDictionaryValue(PdfDictionary value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public PdfDictionary Value { get; }

    /// <inheritdoc/>
    public override string ToString() => $"<< {Value.Count} entries >>";
}
