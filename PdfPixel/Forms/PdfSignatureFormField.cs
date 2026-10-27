using PdfPixel.Objects;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a PDF signature form field.
/// </summary>
public class PdfSignatureFormField : PdfFormField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSignatureFormField"/> class.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing this signature field.</param>
    internal PdfSignatureFormField(PdfObject fieldObject)
        : base(fieldObject, PdfFormFieldType.Signature)
    {
    }

    /// <summary>
    /// Gets a value indicating whether this signature field is signed.
    /// </summary>
    public bool IsSigned => Value is PdfDictionaryValue;
}
