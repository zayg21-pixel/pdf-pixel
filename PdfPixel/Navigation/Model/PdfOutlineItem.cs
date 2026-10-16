using PdfPixel.Actions.Model;
using PdfPixel.Color;
using PdfPixel.Models;
using PdfPixel.Tagging.Model;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// An outline item in the document outline.
/// </summary>
public sealed class PdfOutlineItem
{
    private PdfOutlineItem(
        PdfDictionary dictionary,
        PdfOutlineItem? parent,
        PdfDestinationResolver destinations,
        PdfStructureTree structureTree,
        HashSet<PdfReference> visitedItems)
    {
        Parent = parent;
        Title = dictionary.GetString(PdfTokens.OutlineTitleKey);
        Count = dictionary.GetInteger(PdfTokens.CountKey);
        Destination = destinations.Parse(dictionary, PdfTokens.DestKey);
        Action = PdfAction.FromDictionary(dictionary.GetDictionary(PdfTokens.AKey));
        StructureElement = ReadStructureElement(dictionary, structureTree);
        Color = ReadColor(dictionary.GetArray(PdfTokens.ColorKey));

        int flags = dictionary.GetInteger(PdfTokens.OutlineFlagsKey) ?? 0;
        Flags = (PdfOutlineItemFlags)flags & (PdfOutlineItemFlags.Italic | PdfOutlineItemFlags.Bold);
        Items = ReadItems(dictionary, this, destinations, structureTree, visitedItems);
    }

    /// <summary>
    /// Parent outline item, or <see langword="null"/> for a top-level item.
    /// </summary>
    public PdfOutlineItem? Parent { get; }

    /// <summary>
    /// Text displayed for the item (/Title), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Title { get; }

    /// <summary>
    /// Number of open descendants when the item is open, or the negated number of descendants that would
    /// appear when the item is reopened (/Count); <see langword="null"/> when absent.
    /// </summary>
    public int? Count { get; }

    /// <summary>
    /// Whether the item is open, showing its children.
    /// </summary>
    public bool IsOpen => Count > 0;

    /// <summary>
    /// Destination displayed when the item is activated (/Dest), resolved by this document's
    /// <see cref="PdfDestinationResolver"/>, or <see langword="null"/> when absent.
    /// </summary>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Action performed when the item is activated (/A), or <see langword="null"/> when absent.
    /// </summary>
    public PdfAction? Action { get; }

    /// <summary>
    /// Structure element the item refers to (/SE), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureElement? StructureElement { get; }

    /// <summary>
    /// Color of the item's text (/C), or <see langword="null"/> when absent.
    /// </summary>
    public PdfColor? Color { get; }

    /// <summary>
    /// Style of the item's text (/F).
    /// </summary>
    public PdfOutlineItemFlags Flags { get; }

    /// <summary>
    /// Child outline items (/First, then /Next) in order. Empty if the item has none.
    /// </summary>
    public IReadOnlyList<PdfOutlineItem> Items { get; }

    /// <summary>
    /// Outline items under <paramref name="parentDictionary"/> (/First, then /Next) in order. An item reached
    /// a second time anywhere in the outline ends its chain.
    /// </summary>
    internal static List<PdfOutlineItem> ReadItems(
        PdfDictionary parentDictionary,
        PdfOutlineItem? parent,
        PdfDestinationResolver destinations,
        PdfStructureTree structureTree,
        HashSet<PdfReference> visitedItems)
    {
        List<PdfOutlineItem> items = [];
        PdfDictionary? item = parentDictionary.GetDictionary(PdfTokens.FirstKey);
        PdfReference? reference = parentDictionary.GetReference(PdfTokens.FirstKey);

        while (item != null)
        {
            if (reference != null && !visitedItems.Add(reference.Value))
            {
                break;
            }

            items.Add(new PdfOutlineItem(item, parent, destinations, structureTree, visitedItems));

            reference = item.GetReference(PdfTokens.NextKey);
            item = item.GetDictionary(PdfTokens.NextKey);
        }

        return items;
    }

    private static PdfStructureElement? ReadStructureElement(PdfDictionary dictionary, PdfStructureTree structureTree)
    {
        PdfDictionary? element = dictionary.GetDictionary(PdfTokens.StructureElementKey);
        if (element == null)
        {
            return null;
        }

        return new PdfStructureElement(element, dictionary.GetReference(PdfTokens.StructureElementKey), structureTree);
    }

    private static PdfColor? ReadColor(PdfArray? components)
    {
        if (components == null)
        {
            return null;
        }

        float? red = components.GetFloat(0);
        float? green = components.GetFloat(1);
        float? blue = components.GetFloat(2);
        if (red == null || green == null || blue == null)
        {
            return null;
        }

        return new PdfColor(red.Value, green.Value, blue.Value);
    }
}
