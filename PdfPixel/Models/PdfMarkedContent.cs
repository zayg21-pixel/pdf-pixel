namespace PdfPixel.Models;

/// <summary>
/// Represents a marked content scope with its tag type and type-specific properties.
/// </summary>
public class PdfMarkedContent
{
    /// <summary>
    /// Initializes a new <see cref="PdfMarkedContent"/> with the specified type.
    /// </summary>
    public PdfMarkedContent(PdfMarkedContentType type) => Type = type;

    /// <summary>
    /// Type of the marked content tag.
    /// </summary>
    public PdfMarkedContentType Type { get; }

    /// <summary>
    /// Optional content membership controlling visibility. Set only when <see cref="Type"/>
    /// is <see cref="PdfMarkedContentType.OptionalContent"/>.
    /// </summary>
    public PdfOptionalContentMembership? OptionalContent { get; set; }
}
