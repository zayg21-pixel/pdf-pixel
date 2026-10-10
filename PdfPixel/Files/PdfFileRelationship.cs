using PdfPixel.Text;

namespace PdfPixel.Files;

/// <summary>
/// Relationship between a referring component and its associated file (AFRelationship, PDF 2.0).
/// </summary>
[PdfEnum]
public enum PdfFileRelationship
{
    /// <summary>
    /// Relationship not defined by a specification.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Original source material of the associated content.
    /// </summary>
    [PdfEnumValue("Source")]
    Source,

    /// <summary>
    /// Information used to derive a visual presentation.
    /// </summary>
    [PdfEnumValue("Data")]
    Data,

    /// <summary>
    /// Alternative representation of the content.
    /// </summary>
    [PdfEnumValue("Alternative")]
    Alternative,

    /// <summary>
    /// Supplemental representation of the original source or data.
    /// </summary>
    [PdfEnumValue("Supplement")]
    Supplement,

    /// <summary>
    /// Encrypted payload document.
    /// </summary>
    [PdfEnumValue("EncryptedPayload")]
    EncryptedPayload,

    /// <summary>
    /// Data of the interactive form.
    /// </summary>
    [PdfEnumValue("FormData")]
    FormData,

    /// <summary>
    /// Schema definition of the associated object.
    /// </summary>
    [PdfEnumValue("Schema")]
    Schema,

    /// <summary>
    /// Relationship not known.
    /// </summary>
    [PdfEnumValue("Unspecified")]
    Unspecified
}
