using System.Collections.Generic;

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
