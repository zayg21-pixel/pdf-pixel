using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a union type for PDF values, encapsulating the value and its type.
/// </summary>
/// <typeparam name="T">The underlying value type.</typeparam>
public class PdfValue<T> : IPdfValue, IPdfValue<T>
{

    /// <inheritdoc/>
    public PdfValueType Type { get; }

    /// <inheritdoc/>
    public T Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfValue{T}"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    /// <param name="type">The type of the PDF value.</param>
    internal PdfValue(T value, PdfValueType type)
    {
        Value = value;
        Type = type;
    }

    /// <summary>
    /// Returns a string representation of the PDF value, formatted according to its type.
    /// </summary>
    public override string? ToString()
    {
        return Type switch
        {
            PdfValueType.Null => "null",
            PdfValueType.Name => $"/{Value}",
            PdfValueType.Boolean => Value?.ToString(),
            PdfValueType.String => $"({Value})",
            PdfValueType.Operator => Value?.ToString(),
            PdfValueType.Integer => Value?.ToString(),
            PdfValueType.Real => Value?.ToString(),
            PdfValueType.Reference => Value?.ToString(),
            PdfValueType.Array => (Value is List<IPdfValue> list) ? $"[{list.Count} items]" : "[array]",
            PdfValueType.Dictionary => (Value is PdfDictionary dict) ? $"<< {dict.Count} entries >>" : "<<dictionary>>",
            PdfValueType.InlineStream => "[inline stream]",
            _ => "unknown"
        };
    }
}
