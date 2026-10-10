using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Type of content controlled by an optional content group (/CreatorInfo /Subtype).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentCreatorSubtype
{
    /// <summary>
    /// Subtype not defined by the specification; see <see cref="PdfOptionalContentUsage.RawCreatorSubtype"/>.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Graphic-design or publishing content.
    /// </summary>
    [PdfEnumValue("Artwork")]
    Artwork,

    /// <summary>
    /// Technical designs such as building plans or schematics.
    /// </summary>
    [PdfEnumValue("Technical")]
    Technical
}
