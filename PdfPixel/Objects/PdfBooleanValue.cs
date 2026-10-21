namespace PdfPixel.Objects;

/// <summary>
/// Represents a PDF boolean value.
/// </summary>
public sealed class PdfBooleanValue : PdfValue
{
    /// <summary>
    /// Singleton instance of the <c>true</c> value.
    /// </summary>
    public static readonly PdfBooleanValue True = new(true);

    /// <summary>
    /// Singleton instance of the <c>false</c> value.
    /// </summary>
    public static readonly PdfBooleanValue False = new(false);

    private PdfBooleanValue(bool value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public bool Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
