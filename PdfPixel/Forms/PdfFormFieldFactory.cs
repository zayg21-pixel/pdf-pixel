using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Forms;

/// <summary>
/// Factory class for creating PDF form field instances from PDF objects.
/// </summary>
public static class PdfFormFieldFactory
{
    /// <summary>
    /// Creates a form field instance from a PDF object.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing a form field.</param>
    /// <returns>A concrete form field instance, or null if the object is not a valid field.</returns>
    public static PdfFormField CreateField(PdfObject fieldObject)
    {
        if (fieldObject == null)
        {
            return null;
        }

        var fieldType = fieldObject.Dictionary.GetName(PdfTokens.FieldTypeKey).AsEnum<PdfFormFieldType>();
        if (fieldType == PdfFormFieldType.Unknown)
        {
            return null;
        }

        return fieldType switch
        {
            PdfFormFieldType.Button => new PdfButtonFormField(fieldObject),
            PdfFormFieldType.Text => new PdfTextFormField(fieldObject),
            PdfFormFieldType.Choice => new PdfChoiceFormField(fieldObject),
            PdfFormFieldType.Signature => new PdfSignatureFormField(fieldObject),
            _ => null
        };
    }
}
