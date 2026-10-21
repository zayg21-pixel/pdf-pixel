using PdfPixel.Encryption;
using PdfPixel.Encryption.Decryption;
using PdfPixel.Parsing;
using System.Collections.Generic;

namespace PdfPixel.Objects;

/// <summary>
/// Object index of a file with its trailers, from its cross-reference sections or from a scan of the file.
/// </summary>
internal sealed class PdfCrossReference
{
    private readonly IReadOnlyList<PdfTrailer> _trailers;
    private readonly IReadOnlyList<PdfReference> _catalogs;

    private PdfCrossReference(
        Dictionary<PdfReference, PdfObjectInfo> objectIndex,
        IReadOnlyList<PdfTrailer> trailers,
        IReadOnlyList<PdfReference> catalogs)
    {
        _trailers = trailers;
        _catalogs = catalogs;
        ObjectIndex = objectIndex;
    }

    /// <summary>
    /// Object index of the file.
    /// </summary>
    public Dictionary<PdfReference, PdfObjectInfo> ObjectIndex { get; }

    /// <summary>
    /// The /ID entry of the newest trailer, or null when there is none.
    /// </summary>
    public PdfFileIdentifier? Id => GetNewestTrailer()?.Id;

    /// <summary>
    /// Creates the decryptor of the newest trailer, or returns null when the file is not encrypted.
    /// </summary>
    /// <param name="onCredentialRequested">Called when the empty user password does not decrypt the document.</param>
    public PdfDecryptor? GetDecryptor(PdfCredentialRequestedCallback? onCredentialRequested)
        => GetNewestTrailer()?.GetDecryptor(onCredentialRequested);

    /// <summary>
    /// Resolves the catalog: the newest trailer /Root with a /Pages dictionary, otherwise the newest catalog
    /// object with one, or null when there is neither.
    /// </summary>
    /// <param name="objectCache">The object cache holding <see cref="ObjectIndex"/>.</param>
    public PdfObject? GetRoot(PdfDocumentObjectCache objectCache)
    {
        PdfObject? rootObject = null;
        for (int index = _trailers.Count - 1; index >= 0 && rootObject == null; index--)
        {
            PdfObject? candidate = _trailers[index].GetRoot();
            if (HasPageTree(candidate))
            {
                rootObject = candidate;
            }
        }

        for (int index = _catalogs.Count - 1; index >= 0 && rootObject == null; index--)
        {
            PdfObject? candidate = objectCache.GetObject(_catalogs[index]);
            if (HasPageTree(candidate))
            {
                rootObject = candidate;
            }
        }

        return rootObject;
    }

    /// <summary>
    /// Resolves the /Info entry of the newest trailer.
    /// </summary>
    public PdfDictionary? GetInformation() => GetNewestTrailer()?.GetInformation();

    private PdfTrailer? GetNewestTrailer() => (_trailers.Count > 0) ? _trailers[_trailers.Count - 1] : null;

    /// <summary>
    /// Merges <paramref name="sections"/>, ordered newest first; an entry of a newer section wins.
    /// </summary>
    /// <param name="sections">Cross-reference sections, newest first.</param>
    public static PdfCrossReference FromSections(IReadOnlyList<PdfCrossReferenceSection> sections)
    {
        if (sections.Count == 0)
        {
            return new PdfCrossReference([], [], []);
        }

        Dictionary<PdfReference, PdfObjectInfo> objectIndex = sections[sections.Count - 1].Entries;
        for (int index = sections.Count - 2; index >= 0; index--)
        {
            foreach (KeyValuePair<PdfReference, PdfObjectInfo> entry in sections[index].Entries)
            {
                objectIndex[entry.Key] = entry.Value;
            }
        }

        List<PdfTrailer> trailers = [];
        PdfTrailer? newestTrailer = sections[0].Trailer;
        if (newestTrailer != null)
        {
            trailers.Add(newestTrailer);
        }

        return new PdfCrossReference(objectIndex, trailers, []);
    }

    /// <summary>
    /// Creates the cross-reference of a scan of the file.
    /// </summary>
    /// <param name="objectIndex">Object index built by the scan.</param>
    /// <param name="trailers">Trailers found by the scan, in file order.</param>
    /// <param name="catalogs">References of the catalog objects found by the scan, in file order.</param>
    public static PdfCrossReference FromScan(
        Dictionary<PdfReference, PdfObjectInfo> objectIndex,
        IReadOnlyList<PdfTrailer> trailers,
        IReadOnlyList<PdfReference> catalogs)
    {
        return new(objectIndex, trailers, catalogs);
    }

    private static bool HasPageTree(PdfObject? rootObject) => rootObject?.Dictionary.GetDictionary(PdfTokens.PagesKey) != null;
}
