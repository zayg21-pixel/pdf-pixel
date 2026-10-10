using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// The document structure tree (/StructTreeRoot).
/// </summary>
public sealed class PdfStructureTree
{
    private const int MaxNamespaceRoleMappingSteps = 32;

    private readonly PdfDictionary _dictionary;
    private readonly PdfDictionary? _roleMap;
    private readonly Dictionary<int, ParentTreeEntry> _parentTree = [];
    private readonly Dictionary<PdfReference, PdfStructureNamespace> _namespacesByReference = [];
    private readonly Dictionary<PdfString, PdfDictionary[]> _classMap = [];
    private readonly Dictionary<PdfString, PdfReference?> _elementsById = [];

    internal PdfStructureTree(PdfDictionary dictionary, PdfTreeReader treeReader)
    {
        _dictionary = dictionary;
        _roleMap = dictionary.GetDictionary(PdfTokens.RoleMapKey);
        ParentTreeNextKey = dictionary.GetInteger(PdfTokens.ParentTreeNextKeyKey);
        Namespaces = ReadNamespaces(dictionary.GetArray(PdfTokens.NamespacesKey));
        PronunciationLexicon = PdfFileSpecification.FromArray(dictionary.GetArray(PdfTokens.PronunciationLexiconKey));
        AssociatedFiles = PdfFileSpecification.FromArray(dictionary.GetArray(PdfTokens.AssociatedFilesKey));
        ReadClassMap(dictionary.GetDictionary(PdfTokens.ClassMapKey));

        PdfDictionary? parentTree = dictionary.GetDictionary(PdfTokens.ParentTreeKey);
        if (parentTree != null)
        {
            _parentTree = treeReader.ReadNumberTree(parentTree, ReadParentTreeEntry);
        }

        PdfDictionary? idTree = dictionary.GetDictionary(PdfTokens.IdTreeKey);
        if (idTree != null)
        {
            _elementsById = treeReader.ReadNameTree(idTree, ReadIdTreeEntry);
        }
    }

    /// <summary>
    /// Next structural parent key to assign (/ParentTreeNextKey), or <see langword="null"/> when absent.
    /// </summary>
    public int? ParentTreeNextKey { get; }

    /// <summary>
    /// Namespaces used in the structure tree (/Namespaces, PDF 2.0).
    /// </summary>
    public IReadOnlyList<PdfStructureNamespace> Namespaces { get; }

    /// <summary>
    /// Pronunciation lexicons (/PronunciationLexicon, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfFileSpecification>? PronunciationLexicon { get; }

    /// <summary>
    /// Associated files of the structure tree (/AF, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <summary>
    /// A tree over the catalog's /StructTreeRoot entry, or <see langword="null"/> when the
    /// catalog has none.
    /// </summary>
    internal static PdfStructureTree? FromCatalog(PdfDictionary? catalog, PdfTreeReader treeReader)
    {
        PdfDictionary? structTreeRoot = catalog?.GetDictionary(PdfTokens.StructTreeRootKey);
        return (structTreeRoot != null) ? new PdfStructureTree(structTreeRoot, treeReader) : null;
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
        if (!_parentTree.TryGetValue(structParents, out ParentTreeEntry? entry))
        {
            return null;
        }

        PdfReference?[]? parents = entry.ContentParents;
        if (parents == null || mcid < 0 || mcid >= parents.Length)
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
        if (!_parentTree.TryGetValue(structParent, out ParentTreeEntry? entry))
        {
            return null;
        }

        return LoadElement(entry.ObjectParent);
    }

    /// <summary>
    /// Structure element (/IDTree) whose element identifier (/ID) is <paramref name="id"/>,
    /// or <see langword="null"/> when none is.
    /// </summary>
    public PdfStructureElement? FindById(in PdfString id)
    {
        if (!_elementsById.TryGetValue(id, out PdfReference? element))
        {
            return null;
        }

        return LoadElement(element);
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

    /// <summary>
    /// The structure type <paramref name="structureType"/> of <paramref name="structureNamespace"/> maps onto
    /// through the namespace role maps (/RoleMapNS), or through /RoleMap when <paramref name="structureNamespace"/>
    /// is the default standard structure namespace.
    /// </summary>
    /// <param name="structureType">Structure type as written.</param>
    /// <param name="structureNamespace">Namespace of <paramref name="structureType"/>, or <see langword="null"/> for the default standard structure namespace.</param>
    /// <param name="mappedNamespace">Namespace of the returned type, or <see langword="null"/> for the default standard structure namespace.</param>
    internal PdfString MapRole(in PdfString structureType, PdfStructureNamespace? structureNamespace, out PdfStructureNamespace? mappedNamespace)
    {
        mappedNamespace = structureNamespace;
        if (structureNamespace == null)
        {
            return MapRole(structureType);
        }

        PdfString mapped = structureType;

        for (int step = 0; step < MaxNamespaceRoleMappingSteps; step++)
        {
            if (mappedNamespace?.RoleMapNamespace == null
                || !mappedNamespace.RoleMapNamespace.TryGetValue(mapped, out PdfStructureRoleMapping mapping))
            {
                break;
            }

            PdfStructureNamespace? targetNamespace = GetNamespace(mapping.NamespaceReference);
            if ((mapping.NamespaceReference != null && targetNamespace == null)
                || (mapping.Type == mapped && targetNamespace == mappedNamespace))
            {
                break;
            }

            mapped = mapping.Type;
            mappedNamespace = targetNamespace;
        }

        return mapped;
    }

    /// <summary>
    /// The namespace at <paramref name="reference"/>, or <see langword="null"/> when there is none.
    /// </summary>
    internal PdfStructureNamespace? GetNamespace(PdfReference? reference)
    {
        if (reference == null)
        {
            return null;
        }

        if (_namespacesByReference.TryGetValue(reference.Value, out PdfStructureNamespace? listed))
        {
            return listed;
        }

        PdfDictionary? dictionary = _dictionary.Document.ObjectCache.GetObject(reference.Value)?.Value.AsDictionary();
        return PdfStructureNamespace.FromDictionary(dictionary, reference);
    }

    /// <summary>
    /// Attribute objects of the attribute class <paramref name="className"/> (/ClassMap), or <see langword="null"/> when undefined.
    /// </summary>
    internal PdfDictionary[]? GetClassAttributes(in PdfString className)
        => (_classMap.TryGetValue(className, out PdfDictionary[]? attributes)) ? attributes : null;

    private List<PdfStructureNamespace> ReadNamespaces(PdfArray? namespaces)
    {
        List<PdfStructureNamespace> result = [];
        if (namespaces == null)
        {
            return result;
        }

        for (int index = 0; index < namespaces.Count; index++)
        {
            PdfReference? reference = namespaces.GetReference(index);
            PdfStructureNamespace? structureNamespace = PdfStructureNamespace.FromDictionary(namespaces.GetDictionary(index), reference);
            if (structureNamespace == null)
            {
                continue;
            }

            result.Add(structureNamespace);
            if (reference != null)
            {
                _namespacesByReference[reference.Value] = structureNamespace;
            }
        }

        return result;
    }

    private void ReadClassMap(PdfDictionary? classMap)
    {
        if (classMap == null)
        {
            return;
        }

        foreach (PdfString className in classMap.RawValues.Keys)
        {
            PdfArray? attributeArray = classMap.GetArray(className);
            if (attributeArray == null)
            {
                PdfDictionary? attributes = classMap.GetDictionary(className);
                if (attributes != null)
                {
                    _classMap[className] = new PdfDictionary[] { attributes };
                }

                continue;
            }

            List<PdfDictionary> attributeObjects = new(attributeArray.Count);
            for (int index = 0; index < attributeArray.Count; index++)
            {
                PdfDictionary? attributes = attributeArray.GetDictionary(index);
                if (attributes != null)
                {
                    attributeObjects.Add(attributes);
                }
            }

            _classMap[className] = attributeObjects.ToArray();
        }
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

    private static ParentTreeEntry? ReadParentTreeEntry(PdfArray numbers, int index)
    {
        PdfArray? parents = numbers.GetArray(index);
        if (parents != null)
        {
            var references = new PdfReference?[parents.Count];
            for (int mcid = 0; mcid < parents.Count; mcid++)
            {
                references[mcid] = parents.GetReference(mcid);
            }

            return new ParentTreeEntry(references, null);
        }

        PdfReference? parent = numbers.GetReference(index);
        if (parent == null)
        {
            return null;
        }

        return new ParentTreeEntry(null, parent);
    }

    private static PdfReference? ReadIdTreeEntry(PdfArray names, int index) => names.GetReference(index);

    /// <summary>
    /// A /ParentTree value: the parent of each marked content sequence of a content stream (/StructParents),
    /// or the parent of a single object (/StructParent).
    /// </summary>
    private sealed class ParentTreeEntry
    {
        public ParentTreeEntry(PdfReference?[]? contentParents, PdfReference? objectParent)
        {
            ContentParents = contentParents;
            ObjectParent = objectParent;
        }

        public PdfReference?[]? ContentParents { get; }

        public PdfReference? ObjectParent { get; }
    }
}
