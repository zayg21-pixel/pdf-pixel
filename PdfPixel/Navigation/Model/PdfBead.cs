using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// A bead of an article thread (/Bead): one area of the article on a page.
/// </summary>
public sealed class PdfBead
{
    private readonly PdfDictionary _dictionary;

    private PdfBead(PdfDictionary dictionary)
    {
        _dictionary = dictionary;
        PageReference = dictionary.GetReference(PdfTokens.BeadPageKey);
        Rectangle = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BeadRectangleKey));
    }

    /// <summary>
    /// Page the bead appears on (/P), or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? PageReference { get; }

    /// <summary>
    /// Area of the bead on its page (/R), or <see langword="null"/> when absent.
    /// </summary>
    public PdfRectangle? Rectangle { get; }

    /// <summary>
    /// The bead held by <paramref name="key"/> of <paramref name="owner"/>, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfBead? GetBead(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, FromObject);

    /// <summary>
    /// The bead at <paramref name="index"/> of <paramref name="array"/>, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfBead? GetBead(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, FromObject);

    /// <summary>
    /// Beads of the page (/B) in drawing order; empty when absent.
    /// </summary>
    internal static List<PdfBead> FromPage(PdfDictionary page)
    {
        List<PdfBead> result = [];
        PdfArray? beads = page.GetArray(PdfTokens.BeadsKey);
        if (beads == null)
        {
            return result;
        }

        for (int index = 0; index < beads.Count; index++)
        {
            PdfBead? bead = GetBead(beads, index);
            if (bead != null)
            {
                result.Add(bead);
            }
        }

        return result;
    }

    /// <summary>
    /// Next bead of the thread (/N), or <see langword="null"/> when absent.
    /// </summary>
    public PdfBead? GetNext() => GetBead(_dictionary, PdfTokens.BeadNextKey);

    /// <summary>
    /// Previous bead of the thread (/V), or <see langword="null"/> when absent.
    /// </summary>
    public PdfBead? GetPrevious() => GetBead(_dictionary, PdfTokens.BeadPreviousKey);

    /// <summary>
    /// Thread the bead belongs to (/T), or <see langword="null"/> when no bead before it names one.
    /// </summary>
    public PdfThread? GetThread()
    {
        HashSet<PdfBead> visitedBeads = [];
        var bead = this;

        while (bead != null && visitedBeads.Add(bead))
        {
            PdfThread? thread = PdfThread.GetThread(bead._dictionary, PdfTokens.BeadThreadKey);
            if (thread != null)
            {
                return thread;
            }

            bead = bead.GetPrevious();
        }

        return null;
    }

    private static PdfBead? FromObject(PdfObject beadObject)
    {
        PdfDictionary? dictionary = beadObject.Value.AsDictionary();
        if (dictionary == null)
        {
            return null;
        }

        return new PdfBead(dictionary);
    }
}
