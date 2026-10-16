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
    internal PdfOutlines()
    {
    }

    private PdfOutlines(PdfDictionary dictionary, PdfDestinationResolver destinations, PdfStructureTree structureTree)
    {
        Count = dictionary.GetInteger(PdfTokens.CountKey);
        Items = PdfOutlineItem.ReadItems(dictionary, null, destinations, structureTree, []);
    }

    /// <summary>
    /// Total number of open outline items at all levels (/Count), or <see langword="null"/> when absent.
    /// </summary>
    public int? Count { get; }

    /// <summary>
    /// Top-level outline items (/First, then /Next) in order, or <see langword="null"/> when the document has no outline.
    /// </summary>
    public IReadOnlyList<PdfOutlineItem>? Items { get; }

    /// <summary>
    /// An outline over the catalog's /Outlines entry, without items when the catalog has none.
    /// </summary>
    internal static PdfOutlines FromCatalog(PdfDictionary catalog, PdfDestinationResolver destinations, PdfStructureTree structureTree)
    {
        PdfDictionary? outlines = catalog.GetDictionary(PdfTokens.OutlinesKey);
        if (outlines == null)
        {
            return new PdfOutlines();
        }

        return new PdfOutlines(outlines, destinations, structureTree);
    }
}
