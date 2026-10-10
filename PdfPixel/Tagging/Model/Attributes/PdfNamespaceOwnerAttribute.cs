using PdfPixel.Models;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Attributes owned by a namespace (/O /NSO, PDF 2.0).
/// </summary>
public sealed class PdfNamespaceOwnerAttribute : PdfStructureAttributeBase
{
    internal PdfNamespaceOwnerAttribute(PdfDictionary dictionary, PdfStructureNamespace? @namespace, int? revision)
        : base(PdfStructureAttributeOwner.NamespaceOwner, revision)
    {
        Namespace = @namespace;
        Attributes = dictionary;
    }

    /// <summary>
    /// Namespace owning the attributes (/NS), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureNamespace? Namespace { get; }

    /// <summary>
    /// Attribute names and values defined by <see cref="Namespace"/>.
    /// </summary>
    public PdfDictionary Attributes { get; }
}
