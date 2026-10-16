using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// An article thread (/Thread).
/// </summary>
public sealed class PdfThread
{
    private PdfThread(PdfDictionary dictionary, in PdfReference reference)
    {
        Reference = reference;
        Metadata = PdfMetadata.FromDictionary(dictionary);

        PdfDictionary? information = dictionary.GetDictionary(PdfTokens.ThreadInformationKey);
        if (information != null)
        {
            Information = new PdfDocumentInformation(information);
        }

        Beads = PdfBead.FromThread(dictionary, this);
    }

    /// <summary>
    /// Reference of the thread dictionary.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// Thread information dictionary (/I), or <see langword="null"/> when absent.
    /// </summary>
    public PdfDocumentInformation? Information { get; }

    /// <summary>
    /// Thread metadata stream (/Metadata, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMetadata? Metadata { get; }

    /// <summary>
    /// Beads of the thread (/F, then /N) in reading order. A bead reached a second time through the chain ends it.
    /// </summary>
    public IReadOnlyList<PdfBead> Beads { get; }

    /// <summary>
    /// The thread at <paramref name="index"/> of <paramref name="array"/>, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfThread? GetThread(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, FromObject);

    private static PdfThread? FromObject(PdfObject threadObject)
    {
        PdfDictionary? dictionary = threadObject.Value.AsDictionary();
        if (dictionary == null || threadObject.Reference == null)
        {
            return null;
        }

        return new PdfThread(dictionary, threadObject.Reference.Value);
    }
}
