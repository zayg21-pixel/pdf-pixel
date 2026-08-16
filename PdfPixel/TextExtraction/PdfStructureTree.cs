using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.TextExtraction;

/// <summary>
/// The document structure tree (/StructTreeRoot).
/// </summary>
public sealed class PdfStructureTree
{
    private readonly PdfDictionary _dictionary;

    internal PdfStructureTree(PdfDictionary dictionary)
        => _dictionary = dictionary;

    /// <summary>
    /// Number tree (/ParentTree) mapping structural parent keys onto structure elements,
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfDictionary? ParentTree => _dictionary.GetDictionary(PdfTokens.ParentTreeKey);

    /// <summary>
    /// Next structural parent key to assign (/ParentTreeNextKey), or <see langword="null"/> when absent.
    /// </summary>
    public int? ParentTreeNextKey => _dictionary.GetInteger(PdfTokens.ParentTreeNextKeyKey);

    // TODO: [LOW] parse /IDTree, /ClassMap, /Namespaces, /PronunciationLexicon

    /// <summary>
    /// A tree over the catalog's /StructTreeRoot entry, or <see langword="null"/> when the
    /// catalog has none.
    /// </summary>
    internal static PdfStructureTree? FromCatalog(PdfDictionary? catalog)
    {
        PdfDictionary? structTreeRoot = catalog?.GetDictionary(PdfTokens.StructTreeRootKey);
        return (structTreeRoot != null) ? new PdfStructureTree(structTreeRoot) : null;
    }

    /// <summary>
    /// Root structure elements (/K) in document order.
    /// </summary>
    public IEnumerable<PdfStructureElement> EnumerateRootElements()
    {
        IPdfValue? children = _dictionary.GetValue(PdfTokens.KKey);
        PdfArray? childArray = children.AsArray();

        if (childArray == null)
        {
            PdfDictionary? single = children.AsDictionary();
            if (single != null)
            {
                yield return new PdfStructureElement(single, this);
            }

            yield break;
        }

        for (int index = 0; index < childArray.Count; index++)
        {
            PdfDictionary? element = childArray.GetDictionary(index);
            if (element != null)
            {
                yield return new PdfStructureElement(element, this);
            }
        }
    }

    /// <summary>
    /// The standard structure type the tree's /RoleMap gives for a type,
    /// or the type itself when the map does not name it.
    /// </summary>
    internal PdfString MapRole(in PdfString structureType)
    {
        PdfDictionary? roleMap = _dictionary.GetDictionary(PdfTokens.RoleMapKey);
        if (roleMap == null)
        {
            return structureType;
        }

        PdfString mapped = structureType;

        for (int step = 0; step < roleMap.Count; step++)
        {
            PdfString? next = roleMap.GetName(mapped);
            if (next == null || next.Value == mapped)
            {
                break;
            }

            mapped = next.Value;
        }

        return mapped;
    }
}
