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

    private PdfBead(PdfDictionary dictionary, in PdfReference reference)
    {
        _dictionary = dictionary;
        Reference = reference;
        Thread = PdfThread.GetThread(dictionary, PdfTokens.BeadThreadKey);
        PageReference = dictionary.GetReference(PdfTokens.BeadPageKey);
        Rectangle = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BeadRectangleKey));
    }

    /// <summary>
    /// Reference of the bead dictionary.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// Page the bead appears on (/P), or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? PageReference { get; }

    /// <summary>
    /// Area of the bead on its page (/R), or <see langword="null"/> when absent.
    /// </summary>
    public PdfRectangle? Rectangle { get; }

    /// <summary>
    /// Thread the bead belongs to (/T), or <see langword="null"/> when absent.
    /// </summary>
    public PdfThread? Thread { get; }

    /// <summary>
    /// Returns the next bead of the thread (/N), or <see langword="null"/> when absent.
    /// </summary>
    public PdfBead? GetNext() => GetBead(_dictionary, PdfTokens.BeadNextKey);

    /// <summary>
    /// Returns the previous bead of the thread (/V), or <see langword="null"/> when absent.
    /// </summary>
    public PdfBead? GetPrevious() => GetBead(_dictionary, PdfTokens.BeadPreviousKey);

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

    private static PdfBead? FromObject(PdfObject beadObject)
    {
        PdfDictionary? dictionary = beadObject.Value.AsDictionary();
        if (dictionary == null || beadObject.Reference == null)
        {
            return null;
        }

        return new PdfBead(dictionary, beadObject.Reference.Value);
    }
}
