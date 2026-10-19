using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Builds the cross-reference of a file by scanning the file directly, ignoring the cross-reference
/// structures. Every <c>N G obj</c> declaration, catalog object, <c>trailer</c> dictionary and
/// cross-reference stream in the file is collected; the last declaration of an object reference wins.
/// </summary>
/// <remarks>
/// The scan records offsets and never materializes an object into the cache. Encryption is only known
/// once a trailer has been read, and an object parsed ahead of that point would keep its strings
/// undecrypted, so every object found here is left to be parsed on demand afterwards.
/// </remarks>
internal sealed class PdfXrefRecoveryScanner
{
    private static readonly PdfString Trailer = (PdfString)"trailer"u8;

    private readonly IPdfDocumentInternal _document;
    private readonly ILogger<PdfXrefRecoveryScanner> _logger;
    private readonly PdfCrossReferenceParser _crossReferenceParser;

    public PdfXrefRecoveryScanner(IPdfDocumentInternal document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = document.LoggerFactory.CreateLogger<PdfXrefRecoveryScanner>();
        _crossReferenceParser = new PdfCrossReferenceParser(document);
    }

    /// <summary>
    /// Scans the file and returns the cross-reference it describes.
    /// </summary>
    public PdfCrossReference Scan()
    {
        Dictionary<PdfReference, PdfObjectInfo> objectIndex = [];
        List<PdfObject> crossReferenceStreams = [];
        List<PdfTrailer> trailers = [];
        List<PdfReference> catalogs = [];

        PdfParser parser = new(_document.Stream, _document, PdfParserMode.Recovery);
        parser.Position = _document.Header.Offset;
        int objectsFound = 0;

        while (!parser.IsAtEnd)
        {
            int objectStart = parser.Position;
            PdfObject? obj = parser.ScanObject();

            if (obj != null && obj.Reference != null)
            {
                objectIndex[obj.Reference.Value] = PdfObjectInfo.ForUncompressed(objectStart);
                objectsFound++;

                PdfString? typeName = obj.Dictionary.GetName(PdfTokens.TypeKey);
                if (typeName == PdfTokens.CatalogKey)
                {
                    catalogs.Add(obj.Reference.Value);
                }
                else if (typeName == PdfTokens.XRefKey)
                {
                    crossReferenceStreams.Add(obj);
                    trailers.Add(new PdfTrailer(obj.Dictionary));
                }

                continue;
            }

            parser.Position = objectStart;
            if (parser.TryScanKeyword(Trailer))
            {
                PdfDictionary? trailerDictionary = parser.ReadNextValue()?.AsDictionary();
                if (trailerDictionary != null)
                {
                    trailers.Add(new PdfTrailer(trailerDictionary));
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
            _crossReferenceParser.ApplyCrossReferenceStream(crossReferenceStreams[index], objectIndex);
        }

        _logger.LogInformation(
            "Recovery scanner indexed {Count} object(s), {TrailerCount} trailer dictionary(ies) and {StreamCount} cross-reference stream(s).",
            objectsFound,
            trailers.Count,
            crossReferenceStreams.Count);

        return PdfCrossReference.FromScan(objectIndex, trailers, catalogs);
    }
}
