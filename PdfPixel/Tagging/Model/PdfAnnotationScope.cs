using PdfPixel.Annotations.Model;
using PdfPixel.Files;
using PdfPixel.OptionalContent.Model;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// An annotation enclosing the content its appearance paints.
/// </summary>
public sealed class PdfAnnotationScope : PdfContentScope
{
    private PdfAnnotationScope(
        PdfOptionalContentMembership? optionalContent,
        IReadOnlyList<PdfFileSpecification>? associatedFiles,
        PdfStructureElement? structureElement,
        int? structParent)
        : base(optionalContent, associatedFiles, structureElement)
    {
        StructParent = structParent;
    }

    /// <summary>
    /// Key of the annotation's entry in the structural parent tree (/StructParent), or <see langword="null"/> when absent.
    /// </summary>
    public int? StructParent { get; }

    /// <inheritdoc />
    public override PdfScopeType ScopeType => PdfScopeType.Annotation;

    /// <summary>
    /// Creates the scope of <paramref name="annotation"/>, or <see langword="null"/> when it has no optional content (/OC),
    /// associated files (/AF) or structural parent (/StructParent).
    /// </summary>
    /// <param name="annotation">The drawn annotation.</param>
    /// <param name="structureTree">Structure tree the structural parent is resolved in, or <see langword="null"/> to leave it unresolved.</param>
    internal static PdfAnnotationScope? FromAnnotation(PdfAnnotationBase annotation, PdfStructureTree? structureTree)
    {
        if (annotation.OptionalContent == null && annotation.AssociatedFiles == null && annotation.StructuralParent == null)
        {
            return null;
        }

        PdfStructureElement? structureElement = null;
        if (annotation.StructuralParent != null && structureTree != null)
        {
            structureElement = structureTree.FindParent(annotation.StructuralParent.Value);
        }

        return new PdfAnnotationScope(annotation.OptionalContent, annotation.AssociatedFiles, structureElement, annotation.StructuralParent);
    }

    /// <inheritdoc />
    public override string ToString() => $"Annotation{FormatOptionalContent()}";
}
