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
/// Represents a PDF null value.
/// </summary>
public class NullValue : IPdfValue
{
    /// <summary>
    /// Singleton instance of <see cref="NullValue"/>.
    /// </summary>
    public static readonly NullValue Instance = new NullValue();

    /// <inheritdoc/>
    public PdfValueType Type => PdfValueType.Null;

    /// <summary>
    /// Returns the string representation of the null value.
    /// </summary>
    public override string ToString()
    {
        return "null";
    }
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

/// <summary>
/// Represents a union type for PDF values, encapsulating the value and its type.
/// </summary>
/// <typeparam name="T">The underlying value type.</typeparam>
public class PdfValue<T> : IPdfValue, IPdfValue<T>
{
    private readonly T _value;
    private readonly PdfValueType _type;

    /// <inheritdoc/>
    public PdfValueType Type => _type;
    /// <inheritdoc/>
    public T Value => _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfValue{T}"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    /// <param name="type">The type of the PDF value.</param>
    internal PdfValue(T value, PdfValueType type)
    {
        _value = value;
        _type = type;
    }

    /// <summary>
    /// Returns a string representation of the PDF value, formatted according to its type.
    /// </summary>
    public override string ToString()
    {
        return _type switch
        {
            PdfValueType.Null => "null",
            PdfValueType.Name => $"/{_value}",
            PdfValueType.Boolean => _value.ToString(),
            PdfValueType.String => $"({_value})",
            PdfValueType.Operator => _value.ToString(),
            PdfValueType.Integer => _value.ToString(),
            PdfValueType.Real => _value.ToString(),
            PdfValueType.Reference => _value.ToString(),
            PdfValueType.Array => _value is List<IPdfValue> list ? $"[{list.Count} items]" : "[array]",
            PdfValueType.Dictionary => _value is PdfDictionary dict ? $"<< {dict.Count} entries >>" : "<<dictionary>>",
            PdfValueType.InlineStream => "[inline stream]",
            _ => "unknown"
        };
    }
}

/// <summary>
/// Static factory class for creating <see cref="IPdfValue"/> instances for each PDF value type.
/// </summary>
public static class PdfValueFactory
{
    /// <summary>
    /// Creates a PDF null value.
    /// </summary>
    public static IPdfValue Null() => NullValue.Instance;
    /// <summary>
    /// Creates a PDF name value.
    /// </summary>
    public static IPdfValue<PdfString> Name(PdfString value) => new PdfValue<PdfString>(value, PdfValueType.Name);
    /// <summary>
    /// Creates a PDF string value.
    /// </summary>
    public static IPdfValue<PdfString> String(PdfString value) => new PdfValue<PdfString>(value, PdfValueType.String);
    /// <summary>
    /// Creates a PDF operator value.
    /// </summary>
    public static IPdfValue<PdfString> Operator(PdfString value) => new PdfValue<PdfString>(value, PdfValueType.Operator);
    /// <summary>
    /// Creates a PDF inline stream value.
    /// </summary>
    public static IPdfValue<PdfString> InlineStream(PdfString value) => new PdfValue<PdfString>(value, PdfValueType.InlineStream);
    /// <summary>
    /// Creates a PDF integer value.
    /// </summary>
    public static IPdfValue<int> Integer(int value) => new PdfValue<int>(value, PdfValueType.Integer);
    /// <summary>
    /// Creates a PDF real (floating-point) value.
    /// </summary>
    public static IPdfValue<float> Real(float value) => new PdfValue<float>(value, PdfValueType.Real);
    /// <summary>
    /// Creates a PDF boolean value.
    /// </summary>
    public static IPdfValue<bool> Boolean(bool value) => new PdfValue<bool>(value, PdfValueType.Boolean);
    /// <summary>
    /// Creates a PDF reference value.
    /// </summary>
    public static IPdfValue<PdfReference> Reference(PdfReference value) => new PdfValue<PdfReference>(value, PdfValueType.Reference);
    /// <summary>
    /// Creates a PDF array value.
    /// </summary>
    public static IPdfValue<PdfArray> Array(PdfArray value) => new PdfValue<PdfArray>(value, PdfValueType.Array);
    /// <summary>
    /// Creates a PDF dictionary value.
    /// </summary>
    public static IPdfValue<PdfDictionary> Dictionary(PdfDictionary value) => new PdfValue<PdfDictionary>(value, PdfValueType.Dictionary);
}