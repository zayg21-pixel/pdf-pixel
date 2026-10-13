using PdfPixel.Models;
using PdfPixel.Tagging.Model;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// The document outline (/Outlines).
/// </summary>
public sealed class PdfOutlines
{
    private readonly PdfDictionary _dictionary;

    private PdfOutlines(PdfDictionary dictionary, PdfReference? reference, PdfDestinationResolver destinations, PdfStructureTree? structureTree)
    {
        _dictionary = dictionary;
        Reference = reference;
        Destinations = destinations;
        StructureTree = structureTree;
        Count = dictionary.GetInteger(PdfTokens.CountKey);
    }

    /// <summary>
    /// Total number of open outline items at all levels (/Count), or <see langword="null"/> when absent.
    /// </summary>
    public int? Count { get; }

    /// <summary>
    /// Indirect reference of the outline dictionary, or <see langword="null"/> when it is a direct object.
    /// </summary>
    internal PdfReference? Reference { get; }

    /// <summary>
    /// Resolver reading the destinations of the outline items.
    /// </summary>
    internal PdfDestinationResolver Destinations { get; }

    /// <summary>
    /// Structure tree holding the structure elements of the outline items, or <see langword="null"/> for untagged documents.
    /// </summary>
    internal PdfStructureTree? StructureTree { get; }

    /// <summary>
    /// An outline over the catalog's /Outlines entry, or <see langword="null"/> when the catalog has none.
    /// </summary>
    internal static PdfOutlines? FromCatalog(PdfDictionary catalog, PdfDestinationResolver destinations, PdfStructureTree? structureTree)
    {
        PdfDictionary? outlines = catalog.GetDictionary(PdfTokens.OutlinesKey);
        if (outlines == null)
        {
            return null;
        }

        return new PdfOutlines(outlines, catalog.GetReference(PdfTokens.OutlinesKey), destinations, structureTree);
    }

    /// <summary>
    /// Top-level outline items (/First, then /Next) in order.
    /// </summary>
    public IEnumerable<PdfOutlineItem> EnumerateItems() => EnumerateItems(_dictionary);

    /// <summary>
    /// Outline items under <paramref name="parent"/> (/First, then /Next) in order. An item reached a second
    /// time through the chain ends it.
    /// </summary>
    internal IEnumerable<PdfOutlineItem> EnumerateItems(PdfDictionary parent)
    {
        HashSet<PdfReference> visitedItems = [];
        PdfDictionary? item = parent.GetDictionary(PdfTokens.FirstKey);
        PdfReference? reference = parent.GetReference(PdfTokens.FirstKey);

        while (item != null)
        {
            if (reference != null && !visitedItems.Add(reference.Value))
            {
                yield break;
            }

            yield return new PdfOutlineItem(item, this);

            reference = item.GetReference(PdfTokens.NextKey);
            item = item.GetDictionary(PdfTokens.NextKey);
        }
    }
}
