using PdfPixel.Files;
using PdfPixel.OptionalContent.Model;
using PdfPixel.TextExtraction;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a marked content scope with its tag and type-specific properties.
/// </summary>
public class PdfMarkedContent
{
    /// <summary>
    /// Initializes a new <see cref="PdfMarkedContent"/> with the specified tag name.
    /// </summary>
    public PdfMarkedContent(in PdfString tag) => Tag = tag;

    /// <summary>
    /// Raw tag name from the content stream (e.g. "OC", "Span", "Artifact").
    /// </summary>
    public PdfString Tag { get; }

    /// <summary>
    /// Optional content membership controlling visibility.
    /// Set only when <see cref="Tag"/> is "OC".
    /// </summary>
    public PdfOptionalContentMembership? OptionalContent { get; set; }

    /// <summary>
    /// Files associated with the enclosed content.
    /// Set only when <see cref="Tag"/> is "AF".
    /// </summary>
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; set; }

    /// <summary>
    /// Text-related markup properties extracted from the marked content scope.
    /// Null for optional content (/OC) scopes.
    /// </summary>
    public PdfTextMarkup? TextMarkup { get; set; }
}
