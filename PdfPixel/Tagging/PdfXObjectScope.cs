using PdfPixel.Files;
using PdfPixel.OptionalContent;
using PdfPixel.Resources;
using System.Collections.Generic;

namespace PdfPixel.Tagging;

/// <summary>
/// An XObject invocation enclosing the content the XObject paints.
/// </summary>
public sealed class PdfXObjectScope : PdfContentScope
{
    private PdfXObjectScope(
        PdfOptionalContentMembership? optionalContent,
        IReadOnlyList<PdfFileSpecification>? associatedFiles,
        PdfStructureElement? structureElement,
        int? structParent)
        : base(optionalContent, associatedFiles, structureElement)
    {
        StructParent = structParent;
    }

    /// <summary>
    /// Key of the XObject's entry in the structural parent tree (/StructParent), or <see langword="null"/> when absent.
    /// </summary>
    public int? StructParent { get; }

    /// <inheritdoc />
    public override PdfScopeType ScopeType => PdfScopeType.XObject;

    /// <summary>
    /// Creates the scope of <paramref name="xObject"/>, or <see langword="null"/> when it has no optional content (/OC),
    /// associated files (/AF) or structural parent (/StructParent).
    /// </summary>
    /// <param name="xObject">The invoked XObject.</param>
    /// <param name="structureTree">Structure tree the structural parent is resolved in, or <see langword="null"/> to leave it unresolved.</param>
    internal static PdfXObjectScope? FromXObject(PdfXObject xObject, PdfStructureTree? structureTree)
    {
        if (xObject.OptionalContent == null && xObject.AssociatedFiles == null && xObject.StructParent == null)
        {
            return null;
        }

        PdfStructureElement? structureElement = null;
        if (xObject.StructParent != null && structureTree != null)
        {
            structureElement = structureTree.FindParent(xObject.StructParent.Value);
        }

        return new PdfXObjectScope(xObject.OptionalContent, xObject.AssociatedFiles, structureElement, xObject.StructParent);
    }

    /// <inheritdoc />
    public override string ToString() => $"XObject{FormatOptionalContent()}";
}
