using System.Globalization;

namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF real (floating-point) number.
/// </summary>
public sealed class PdfRealValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRealValue"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    internal PdfRealValue(float value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public float Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
