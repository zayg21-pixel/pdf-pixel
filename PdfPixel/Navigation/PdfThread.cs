using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Navigation;

/// <summary>
/// An article thread (/Thread).
/// </summary>
public sealed class PdfThread
{
    private readonly PdfDictionary _dictionary;

    private PdfThread(PdfDictionary dictionary, in PdfReference reference)
    {
        _dictionary = dictionary;
        Reference = reference;
        Metadata = PdfMetadata.FromDictionary(dictionary);

        PdfDictionary? information = dictionary.GetDictionary(PdfTokens.ThreadInformationKey);
        if (information != null)
        {
            Information = new PdfDocumentInformation(information);
        }
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
    /// Returns the first bead of the thread (/F), or <see langword="null"/> when absent.
    /// </summary>
    public PdfBead? GetFirstBead() => PdfBead.GetBead(_dictionary, PdfTokens.ThreadFirstBeadKey);

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
