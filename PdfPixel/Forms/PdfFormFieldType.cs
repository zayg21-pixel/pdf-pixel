using PdfPixel.Text;

namespace PdfPixel.Forms;

/// <summary>
/// Represents the type of a PDF form field.
/// </summary>
[PdfEnum]
public enum PdfFormFieldType
{
    /// <summary>
    /// Unknown or unsupported field type.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Button field (push button, checkbox, radio button).
    /// </summary>
    [PdfEnumValue("Btn")]
    Button,

    /// <summary>
    /// Text field (single line, multiline, password, file select).
    /// </summary>
    [PdfEnumValue("Tx")]
    Text,

    /// <summary>
    /// Choice field (list box, combo box).
    /// </summary>
    [PdfEnumValue("Ch")]
    Choice,

    /// <summary>
    /// Signature field.
    /// </summary>
    [PdfEnumValue("Sig")]
    Signature
}
