namespace PdfPixel.Objects;

/// <summary>
/// Represents a PDF array object.
/// </summary>
public sealed class PdfArrayValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfArrayValue"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    internal PdfArrayValue(PdfArray value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public PdfArray Value { get; }

    /// <inheritdoc/>
    public override string ToString() => "[array]";
}
