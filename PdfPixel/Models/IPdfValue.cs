namespace PdfPixel.Models;

/// <summary>
/// Represents a value in a PDF document, providing its type information.
/// </summary>
public interface IPdfValue
{
    /// <summary>
    /// Gets the type of the PDF value.
    /// </summary>
    PdfValueType Type { get; }
}
