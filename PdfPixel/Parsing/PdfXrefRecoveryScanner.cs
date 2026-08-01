using System;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Fallback xref recovery scanner. When normal xref loading fails or produces an incomplete result,
/// scans the entire file using PdfParser.ReadObject() to rebuild the object index from physical byte
/// positions. Identifies the document catalog directly from object content so no trailer is needed.
/// By the time this runs, the existing xref table is known to be untrustworthy, so entries found by
/// the scan replace whatever PdfXrefLoader indexed; later occurrences in the file (e.g. from
/// incremental updates) win, since the scan proceeds in physical file order.
/// </summary>
internal sealed class PdfXrefRecoveryScanner
{
    private readonly IPdfDocumentInternal _document;
    private readonly ILogger<PdfXrefRecoveryScanner> _logger;
    private readonly PdfTrailerParser _trailerParser;

    public PdfXrefRecoveryScanner(IPdfDocumentInternal document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = document.LoggerFactory.CreateLogger<PdfXrefRecoveryScanner>();
        _trailerParser = new PdfTrailerParser(document);
    }

    public void Scan()
    {
        // The decryptor must be recovered before the object-parsing loop below runs, not after --
        // strings are decrypted at parse time (see PdfParser.String.cs), driven by whatever
        // Decryptor is set at that moment. Recovering it first lets the single parser instance
        // below decrypt every object's strings inline as they're read.
        RecoverDecryptor();

        PdfParser parser = new(_document.Stream, _document, allowReferences: true, decrypt: true);
        parser.Position = 0;
        int objectsFound = 0;

        while (!parser.IsAtEnd)
        {
            int objectStart = parser.Position;
            PdfObject? obj = parser.ReadObject();

            if (obj == null)
            {
                parser.Position = objectStart + 1;
                continue;
            }

            _document.ObjectCache.SetObject(obj, objectStart);
            objectsFound++;

            if (_document.RootObject == null
                && obj.Dictionary?.GetName(PdfTokens.TypeKey) == PdfTokens.CatalogKey)
            {
                _document.RootObject = obj;
            }
        }

        _logger.LogInformation("Recovery scanner indexed {Count} object(s).", objectsFound);
    }

    // The classic xref/trailer parse (PdfXrefLoader) is what normally locates /Encrypt and /ID and
    // wires up the decryptor via PdfTrailerParser.TrySetDecryptor. When that parse fails before ever
    // reaching the trailer keyword, the decryptor is never set, and every subsequently decoded stream
    // is fed to its filters (e.g. Flate) still encrypted, producing errors far removed from the real
    // cause. The trailer dictionary is always literal cleartext -- PDF encryption applies only to
    // string and stream values inside numbered objects, never to xref/trailer structure -- so it can
    // still be recovered by scanning for the last "trailer" keyword in the file, independent of
    // whether the xref table around it parsed correctly.
    private void RecoverDecryptor()
    {
        if (_document.Decryptor != null)
        {
            return;
        }

        long trailerPosition = PdfByteScanner.LocateLast(_document.Stream, PdfTokens.Trailer.Value.Span);
        if (trailerPosition < 0)
        {
            return;
        }

        PdfParser parser = new(_document.Stream, _document, allowReferences: true, decrypt: false);
        parser.Position = (int)trailerPosition + PdfTokens.Trailer.Value.Length;

        PdfDictionary? trailerDict = parser.ReadNextValue()?.AsDictionary();
        if (trailerDict != null)
        {
            _trailerParser.TrySetDecryptor(trailerDict);
        }
    }
}
