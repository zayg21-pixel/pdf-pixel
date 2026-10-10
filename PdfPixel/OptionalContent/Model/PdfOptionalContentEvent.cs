using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Situation in which optional content states are applied (/Event).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentEvent
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// The document is viewed.
    /// </summary>
    [PdfEnumValue("View")]
    View,

    /// <summary>
    /// The document is printed.
    /// </summary>
    [PdfEnumValue("Print")]
    Print,

    /// <summary>
    /// The document is exported to a format that does not support optional content.
    /// </summary>
    [PdfEnumValue("Export")]
    Export
}
