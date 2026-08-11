using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Rebuilds the object index and locates the document catalog by scanning the file directly, ignoring
/// the cross-reference structures. Every <c>N G obj</c> declaration, <c>trailer</c> dictionary and
/// cross-reference stream in the file is collected; the last declaration of an object reference wins.
/// The root is taken from the newest trailer that resolves to a page tree, falling back to a catalog
/// object and finally to any object carrying a <c>/Root</c> entry.
/// </summary>
/// <remarks>
/// The scan records offsets and never materializes an object into the cache. Encryption is only known
/// once a trailer has been read, and an object parsed ahead of that point would keep its strings
/// undecrypted, so every object found here is left to be parsed on demand afterwards.
/// </remarks>
internal sealed class PdfXrefRecoveryScanner
{
    private readonly IPdfDocumentInternal _document;
    private readonly ILogger<PdfXrefRecoveryScanner> _logger;
    private readonly PdfTrailerParser _trailerParser;
    private readonly PdfXrefLoader _xrefLoader;

    public PdfXrefRecoveryScanner(IPdfDocumentInternal document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = document.LoggerFactory.CreateLogger<PdfXrefRecoveryScanner>();
        _trailerParser = new PdfTrailerParser(document);
        _xrefLoader = new PdfXrefLoader(document);
    }

    public void Scan()
    {
        List<PdfObject> crossReferenceStreams = [];
        List<PdfDictionary> trailerDictionaries = [];
        PdfReference? catalogReference = null;
        PdfReference? referenceWithRoot = null;

        PdfParser parser = new(_document.Stream, _document, allowReferences: true, decrypt: false);
        parser.Position = _document.HeaderOffset;
        int objectsFound = 0;

        while (!parser.IsAtEnd)
        {
            int objectStart = parser.Position;
            PdfObject? obj = parser.ScanObject();

            if (obj != null)
            {
                _document.ObjectCache.SetObjectOffset(obj.Reference, objectStart);
                objectsFound++;

                PdfString typeName = obj.Dictionary.GetName(PdfTokens.TypeKey);
                if (typeName == PdfTokens.CatalogKey)
                {
                    catalogReference = obj.Reference;
                }
                else if (typeName == PdfTokens.XRefKey)
                {
                    crossReferenceStreams.Add(obj);
                    trailerDictionaries.Add(obj.Dictionary);
                }

                if (referenceWithRoot == null && obj.Dictionary.HasKey(PdfTokens.RootKey))
                {
                    referenceWithRoot = obj.Reference;
                }

                continue;
            }

            parser.Position = objectStart;
            if (parser.TryScanKeyword(PdfTokens.Trailer))
            {
                PdfDictionary? trailerDictionary = parser.ReadNextValue()?.AsDictionary();
                if (trailerDictionary != null)
                {
                    trailerDictionaries.Add(trailerDictionary);
                    continue;
                }
            }

            parser.Position = objectStart + 1;
        }

        // Compressed objects carry no header of their own, so the scan above cannot see them. The
        // cross-reference streams it did find describe where they live, and their entries are applied
        // only where the scan left a gap. Newest first: a stream late in the file belongs to a later
        // incremental update, and an entry already applied is never replaced.
        for (int index = crossReferenceStreams.Count - 1; index >= 0; index--)
        {
            _xrefLoader.ApplyCrossReferenceStream(crossReferenceStreams[index]);
        }

        _logger.LogInformation(
            "Recovery scanner indexed {Count} object(s), {TrailerCount} trailer dictionary(ies) and {StreamCount} cross-reference stream(s).",
            objectsFound,
            trailerDictionaries.Count,
            crossReferenceStreams.Count);

        RecoverDecryptor(trailerDictionaries);
        SelectRootObject(trailerDictionaries, catalogReference, referenceWithRoot);
    }

    /// <summary>
    /// Establishes the decryptor from the newest trailer that carries an <c>/Encrypt</c> entry. Runs
    /// once the index is complete, since the encryption dictionary is itself an indirect object.
    /// </summary>
    private void RecoverDecryptor(List<PdfDictionary> trailerDictionaries)
    {
        if (_document.Decryptor != null)
        {
            return;
        }

        for (int index = trailerDictionaries.Count - 1; index >= 0; index--)
        {
            _trailerParser.TrySetDecryptor(trailerDictionaries[index]);

            if (_document.Decryptor != null)
            {
                return;
            }
        }
    }

    private void SelectRootObject(List<PdfDictionary> trailerDictionaries, PdfReference? catalogReference, PdfReference? referenceWithRoot)
    {
        if (LeadsToPageTree(_document.RootObject))
        {
            return;
        }

        for (int index = trailerDictionaries.Count - 1; index >= 0; index--)
        {
            PdfObject? rootObject = trailerDictionaries[index].GetObject(PdfTokens.RootKey);
            if (LeadsToPageTree(rootObject))
            {
                _document.RootObject = rootObject;
                return;
            }
        }

        if (catalogReference != null)
        {
            _document.RootObject = _document.ObjectCache.GetObject(catalogReference.Value);
            if (_document.RootObject != null)
            {
                return;
            }
        }

        if (referenceWithRoot != null)
        {
            PdfObject? rootHolder = _document.ObjectCache.GetObject(referenceWithRoot.Value);
            PdfObject? rootObject = rootHolder?.Dictionary.GetObject(PdfTokens.RootKey);
            if (rootObject != null)
            {
                _document.RootObject = rootObject;
                return;
            }
        }

        _logger.LogWarning("Recovery scan found no trailer, catalog or /Root entry that yields a document root.");
    }

    private static bool LeadsToPageTree(PdfObject? rootObject)
        => rootObject?.Dictionary.GetDictionary(PdfTokens.PagesKey) != null;
}
