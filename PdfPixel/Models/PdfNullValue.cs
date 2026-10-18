namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF null value.
/// </summary>
public sealed class PdfNullValue : PdfValue
{
    /// <summary>
    /// Singleton instance of <see cref="PdfNullValue"/>.
    /// </summary>
    public static readonly PdfNullValue Instance = new();

    private PdfNullValue()
    {
    }

    /// <summary>
    /// Returns the string representation of the null value.
    /// </summary>
    public override string ToString() => "null";
}
