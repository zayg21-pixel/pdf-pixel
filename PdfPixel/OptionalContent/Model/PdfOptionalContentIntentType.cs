using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Intended use of optional content (/Intent).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentIntentType
{
    /// <summary>
    /// Intent not defined by the specification; see <see cref="PdfOptionalContentIntent.RawType"/>.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Content intended for interactive viewing.
    /// </summary>
    [PdfEnumValue("View")]
    View,

    /// <summary>
    /// Content representing a document designer's structural organization of artwork.
    /// </summary>
    [PdfEnumValue("Design")]
    Design,

    /// <summary>
    /// Set of all intents; valid only in a configuration's /Intent.
    /// </summary>
    [PdfEnumValue("All")]
    All
}
