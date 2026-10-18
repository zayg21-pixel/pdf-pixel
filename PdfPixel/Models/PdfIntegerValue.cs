using System.Globalization;

namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF integer number.
/// </summary>
public sealed class PdfIntegerValue : PdfValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfIntegerValue"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    internal PdfIntegerValue(int value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public int Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
