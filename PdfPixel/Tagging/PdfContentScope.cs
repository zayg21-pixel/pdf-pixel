using PdfPixel.Files;
using PdfPixel.OptionalContent;
using System.Collections.Generic;

namespace PdfPixel.Tagging;

/// <summary>
/// A scope carrying optional content, associated files and the structure element of the enclosed content.
/// </summary>
public abstract class PdfContentScope : PdfScope, IPdfAssociatedFilesHost
{
    /// <summary>
    /// Initializes a new <see cref="PdfContentScope"/> with the optional content membership, associated files and
    /// structure element of the enclosed content.
    /// </summary>
    private protected PdfContentScope(
        PdfOptionalContentMembership? optionalContent,
        IReadOnlyList<PdfFileSpecification>? associatedFiles,
        PdfStructureElement? structureElement)
    {
        OptionalContent = optionalContent;
        AssociatedFiles = associatedFiles;
        StructureElement = structureElement;
    }

    /// <summary>
    /// Optional content membership controlling visibility of the enclosed content, or <see langword="null"/> when absent.
    /// </summary>
    public PdfOptionalContentMembership? OptionalContent { get; }

    /// <inheritdoc />
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <summary>
    /// Parent structure element of the enclosed content (/ParentTree), or <see langword="null"/> when not linked to the structure tree.
    /// </summary>
    public PdfStructureElement? StructureElement { get; }

    private protected string FormatOptionalContent()
    {
        if (OptionalContent == null)
        {
            return string.Empty;
        }

        string groups = string.Join(",", OptionalContent.Groups);
        string expression = (OptionalContent.VisibilityExpression != null) ? " VE" : string.Empty;
        return $" OC[{groups}] {OptionalContent.VisibilityPolicy}{expression}";
    }
}
