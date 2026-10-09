using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// The document structure tree (/StructTreeRoot).
/// </summary>
public sealed class PdfStructureTree
{
    private readonly PdfDictionary _dictionary;
    private readonly PdfDictionary? _roleMap;
    private readonly Dictionary<int, PdfReference?[]> _contentParents = [];
    private readonly Dictionary<int, PdfReference> _objectParents = [];

    internal PdfStructureTree(PdfDictionary dictionary)
    {
        _dictionary = dictionary;
        _roleMap = dictionary.GetDictionary(PdfTokens.RoleMapKey);
        ParentTreeNextKey = dictionary.GetInteger(PdfTokens.ParentTreeNextKeyKey);

        PdfDictionary? parentTree = dictionary.GetDictionary(PdfTokens.ParentTreeKey);
        if (parentTree != null)
        {
            HashSet<PdfReference> visitedNodes = [];
            ReadParentTreeNode(parentTree, visitedNodes);
        }
    }

    /// <summary>
    /// Next structural parent key to assign (/ParentTreeNextKey), or <see langword="null"/> when absent.
    /// </summary>
    public int? ParentTreeNextKey { get; }

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
                yield return new PdfStructureElement(single, _dictionary.GetReference(PdfTokens.KKey), this);
            }

            yield break;
        }

        for (int index = 0; index < childArray.Count; index++)
        {
            PdfDictionary? element = childArray.GetDictionary(index);
            if (element != null)
            {
                yield return new PdfStructureElement(element, childArray.GetReference(index), this);
            }
        }
    }

    /// <summary>
    /// Structure element (/ParentTree) holding marked content <paramref name="mcid"/> of the content stream
    /// whose /StructParents is <paramref name="structParents"/>, or <see langword="null"/> when none does.
    /// </summary>
    public PdfStructureElement? FindParent(int structParents, int mcid)
    {
        if (!_contentParents.TryGetValue(structParents, out PdfReference?[]? parents)
            || mcid < 0
            || mcid >= parents.Length)
        {
            return null;
        }

        return LoadElement(parents[mcid]);
    }

    /// <summary>
    /// Structure element (/ParentTree) of the object whose /StructParent is <paramref name="structParent"/>,
    /// or <see langword="null"/> when none is.
    /// </summary>
    public PdfStructureElement? FindParent(int structParent)
    {
        if (!_objectParents.TryGetValue(structParent, out PdfReference parent))
        {
            return null;
        }

        return LoadElement(parent);
    }

    /// <summary>
    /// The standard structure type the tree's /RoleMap gives for a type,
    /// or the type itself when the map does not name it.
    /// </summary>
    internal PdfString MapRole(in PdfString structureType)
    {
        if (_roleMap == null)
        {
            return structureType;
        }

        PdfString mapped = structureType;

        for (int step = 0; step < _roleMap.Count; step++)
        {
            PdfString? next = _roleMap.GetName(mapped);
            if (next == null || next.Value == mapped)
            {
                break;
            }

            mapped = next.Value;
        }

        return mapped;
    }

    private PdfStructureElement? LoadElement(PdfReference? reference)
    {
        if (reference == null)
        {
            return null;
        }

        PdfDictionary? element = _dictionary.Document.ObjectCache.GetObject(reference.Value)?.Value.AsDictionary();
        if (element == null)
        {
            return null;
        }

        return new PdfStructureElement(element, reference, this);
    }

    private void ReadParentTreeNode(PdfDictionary node, HashSet<PdfReference> visitedNodes)
    {
        PdfArray? numbers = node.GetArray(PdfTokens.NumsKey);
        if (numbers != null)
        {
            for (int index = 0; index + 1 < numbers.Count; index += 2)
            {
                int? key = numbers.GetInteger(index);
                if (key == null)
                {
                    continue;
                }

                PdfArray? parents = numbers.GetArray(index + 1);
                if (parents != null)
                {
                    var references = new PdfReference?[parents.Count];
                    for (int mcid = 0; mcid < parents.Count; mcid++)
                    {
                        references[mcid] = parents.GetReference(mcid);
                    }

                    _contentParents[key.Value] = references;
                    continue;
                }

                PdfReference? parent = numbers.GetReference(index + 1);
                if (parent != null)
                {
                    _objectParents[key.Value] = parent.Value;
                }
            }
        }

        PdfArray? kids = node.GetArray(PdfTokens.KidsKey);
        if (kids == null)
        {
            return;
        }

        for (int index = 0; index < kids.Count; index++)
        {
            PdfReference? kidReference = kids.GetReference(index);
            if (kidReference != null && !visitedNodes.Add(kidReference.Value))
            {
                continue;
            }

            PdfDictionary? kid = kids.GetDictionary(index);
            if (kid != null)
            {
                ReadParentTreeNode(kid, visitedNodes);
            }
        }
    }
}
