using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Document-level structure tree parsed from the catalog's /StructTreeRoot entry.
/// Contains the root structure elements defining the document's logical reading order.
/// </summary>
public class PdfStructureTree
{
    private readonly Dictionary<(PdfReference pageRef, int mcid), PdfStructureElement> _mcidMap = [];
    private int _nextIndex;

    private PdfStructureTree()
    {
    }

    // TODO: [LOW] parse /RoleMap, /IDTree, /ClassMap

    /// <summary>
    /// Root structure elements in document reading order.
    /// </summary>
    public List<PdfStructureElement> RootElements { get; } = [];

    /// <summary>
    /// Looks up the structure element that owns the given marked content identifier on the specified page.
    /// Returns <see langword="null"/> if the MCID is not present in the structure tree.
    /// </summary>
    internal PdfStructureElement? FindByMcid(PdfReference pageRef, int mcid)
        => (_mcidMap.TryGetValue((pageRef, mcid), out PdfStructureElement? element)) ? element : null;

    /// <summary>
    /// Parses the structure tree from the catalog's /StructTreeRoot dictionary.
    /// Returns <see langword="null"/> if no structure tree is present.
    /// </summary>
    internal static PdfStructureTree? FromDictionary(PdfDictionary? structTreeRoot, IPdfDocumentInternal document)
    {
        if (structTreeRoot == null)
        {
            return null;
        }

        PdfStructureTree tree = new();

        PdfArray? childrenArray = structTreeRoot.GetArray(PdfTokens.KKey);
        if (childrenArray != null)
        {
            for (int i = 0; i < childrenArray.Count; i++)
            {
                PdfDictionary? kidDict = childrenArray.GetDictionary(i);
                if (kidDict != null)
                {
                    PdfStructureElement? element = PdfStructureElement.FromDictionary(kidDict, document);
                    if (element != null)
                    {
                        tree.RootElements.Add(element);
                    }
                }
            }
        }
        else
        {
            PdfDictionary? singleKid = structTreeRoot.GetDictionary(PdfTokens.KKey);
            if (singleKid != null)
            {
                PdfStructureElement? element = PdfStructureElement.FromDictionary(singleKid, document);
                if (element != null)
                {
                    tree.RootElements.Add(element);
                }
            }
        }

        foreach (PdfStructureElement rootElement in tree.RootElements)
        {
            tree.IndexAndMap(rootElement);
        }

        return tree;
    }

    private void IndexAndMap(PdfStructureElement element)
    {
        element.Index = _nextIndex++;
        foreach (PdfStructureNode child in element.Children)
        {
            if (child.Type == PdfStructureNodeType.Mcid && child.Mcid != null)
            {
                PdfReference? pageRef = child.PageReference ?? element.PageReference;
                if (pageRef != null)
                {
                    _mcidMap[(pageRef.Value, child.Mcid.Value)] = element;
                }
            }
            else if (child.Type == PdfStructureNodeType.Element && child.Element != null)
            {
                IndexAndMap(child.Element);
            }
        }
    }
}
