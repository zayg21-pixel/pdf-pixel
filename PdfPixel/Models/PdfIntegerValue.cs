using System.Globalization;

namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF integer number.
/// </summary>
public sealed class PdfIntegerValue : PdfValue
{
    /// <summary>
    /// Singleton instance of the integer 0.
    /// </summary>
    public static readonly PdfIntegerValue Zero = new(0);

    /// <summary>
    /// Singleton instance of the integer 1.
    /// </summary>
    public static readonly PdfIntegerValue One = new(1);

    private PdfIntegerValue(int value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Returns the shared instance for 0 and 1, or a new instance for any other value.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    internal static PdfIntegerValue FromInteger(int value)
    {
        if (value == 0)
        {
            return Zero;
        }
        else if (value == 1)
        {
            return One;
        }

        return new PdfIntegerValue(value);
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
