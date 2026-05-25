namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF null value.
/// </summary>
public class NullValue : IPdfValue
{
    /// <summary>
    /// Singleton instance of <see cref="NullValue"/>.
    /// </summary>
    public static readonly NullValue Instance = new();

    /// <inheritdoc/>
    public PdfValueType Type => PdfValueType.Null;

    /// <summary>
    /// Returns the string representation of the null value.
    /// </summary>
    public override string ToString() => "null";
}
