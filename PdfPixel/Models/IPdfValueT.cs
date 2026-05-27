namespace PdfPixel.Models;

/// <summary>
/// Represents a strongly-typed PDF value.
/// </summary>
/// <typeparam name="T">The underlying value type.</typeparam>
public interface IPdfValue<T> : IPdfValue
{
    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    T Value { get; }
}
