using PdfPixel.Models;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a PDF signature form field.
/// </summary>
/// <remarks>
/// Signature fields contain digital signatures that can be used to authenticate
/// the signer and verify document integrity.
/// </remarks>
public class PdfSignatureFormField : PdfFormField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSignatureFormField"/> class.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing this signature field.</param>
    public PdfSignatureFormField(PdfObject fieldObject)
        : base(fieldObject, PdfFormFieldType.Signature)
    {
    }

    /// <summary>
    /// Gets a value indicating whether this signature field is signed.
    /// </summary>
    public bool IsSigned => Value != null && Value.Type == PdfValueType.Dictionary;
}
