using PdfPixel.Models;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// Target of a structure type in a namespace role map (/RoleMapNS).
/// </summary>
public readonly struct PdfStructureRoleMapping
{
    internal PdfStructureRoleMapping(in PdfString type, PdfReference? namespaceReference)
    {
        Type = type;
        NamespaceReference = namespaceReference;
    }

    /// <summary>
    /// Structure type the source type maps onto.
    /// </summary>
    public PdfString Type { get; }

    /// <summary>
    /// Namespace of <see cref="Type"/>, or <see langword="null"/> for the default standard structure namespace.
    /// </summary>
    public PdfReference? NamespaceReference { get; }
}
