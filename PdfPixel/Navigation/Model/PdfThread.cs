using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// An article thread (/Thread).
/// </summary>
public sealed class PdfThread
{
    private readonly PdfDictionary _dictionary;

    private PdfThread(PdfDictionary dictionary)
    {
        _dictionary = dictionary;
        Metadata = PdfMetadata.FromDictionary(dictionary);

        PdfDictionary? information = dictionary.GetDictionary(PdfTokens.ThreadInformationKey);
        if (information != null)
        {
            Information = new PdfDocumentInformation(information);
        }
    }

    /// <summary>
    /// Thread information dictionary (/I), or <see langword="null"/> when absent.
    /// </summary>
    public PdfDocumentInformation? Information { get; }

    /// <summary>
    /// Thread metadata stream (/Metadata, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMetadata? Metadata { get; }

    /// <summary>
    /// The thread held by <paramref name="key"/> of <paramref name="owner"/>, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfThread? GetThread(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, FromObject);

    /// <summary>
    /// The thread at <paramref name="index"/> of <paramref name="array"/>, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfThread? GetThread(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, FromObject);

    /// <summary>
    /// Article threads of the catalog (/Threads) in order; empty when absent.
    /// </summary>
    internal static List<PdfThread> FromCatalog(PdfDictionary catalog)
    {
        List<PdfThread> result = [];
        PdfArray? threads = catalog.GetArray(PdfTokens.ThreadsKey);
        if (threads == null)
        {
            return result;
        }

        for (int index = 0; index < threads.Count; index++)
        {
            PdfThread? thread = GetThread(threads, index);
            if (thread != null)
            {
                result.Add(thread);
            }
        }

        return result;
    }

    /// <summary>
    /// Beads of the thread (/F, then /N) in reading order. A bead reached a second time through the chain ends it.
    /// </summary>
    public IEnumerable<PdfBead> EnumerateBeads()
    {
        HashSet<PdfBead> visitedBeads = [];
        PdfBead? bead = PdfBead.GetBead(_dictionary, PdfTokens.ThreadFirstBeadKey);

        while (bead != null && visitedBeads.Add(bead))
        {
            yield return bead;
            bead = bead.GetNext();
        }
    }

    private static PdfThread? FromObject(PdfObject threadObject)
    {
        PdfDictionary? dictionary = threadObject.Value.AsDictionary();
        if (dictionary == null)
        {
            return null;
        }

        return new PdfThread(dictionary);
    }
}
