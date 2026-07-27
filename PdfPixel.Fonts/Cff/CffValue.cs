namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Represents a union type for CFF values, encapsulating the value and its type.
/// </summary>
/// <typeparam name="T">The underlying value type.</typeparam>
public class CffValue<T> : ICffValue
{
    /// <inheritdoc/>
    public CffValueType Type { get; }

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CffValue{T}"/> class.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    /// <param name="type">The type of the CFF value.</param>
    internal CffValue(T value, CffValueType type)
    {
        Value = value;
        Type = type;
    }
}
