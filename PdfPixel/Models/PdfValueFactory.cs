namespace PdfPixel.Models;

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
    public static IPdfValue<PdfString> Name(in PdfString value) => new PdfValue<PdfString>(value, PdfValueType.Name);

    /// <summary>
    /// Creates a PDF string value.
    /// </summary>
    public static IPdfValue<PdfString> String(in PdfString value) => new PdfValue<PdfString>(value, PdfValueType.String);

    /// <summary>
    /// Creates a PDF operator value.
    /// </summary>
    public static IPdfValue<PdfString> Operator(in PdfString value) => new PdfValue<PdfString>(value, PdfValueType.Operator);

    /// <summary>
    /// Creates a PDF inline stream value.
    /// </summary>
    public static IPdfValue<PdfString> InlineStream(in PdfString value) => new PdfValue<PdfString>(value, PdfValueType.InlineStream);

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
    public static IPdfValue<PdfReference> Reference(in PdfReference value) => new PdfValue<PdfReference>(value, PdfValueType.Reference);

    /// <summary>
    /// Creates a PDF array value.
    /// </summary>
    public static IPdfValue<PdfArray> Array(PdfArray value) => new PdfValue<PdfArray>(value, PdfValueType.Array);

    /// <summary>
    /// Creates a PDF dictionary value.
    /// </summary>
    public static IPdfValue<PdfDictionary> Dictionary(PdfDictionary value) => new PdfValue<PdfDictionary>(value, PdfValueType.Dictionary);
}
