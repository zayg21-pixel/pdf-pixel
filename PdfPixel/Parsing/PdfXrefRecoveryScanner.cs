using System;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Rebuilds the object index and locates the document catalog by scanning the file directly for
/// <c>N G obj</c> declarations. The last occurrence of a given object reference wins.
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

            if (_document.ObjectCache.ObjectIndex.ContainsKey(obj.Reference))
            {
                _logger.LogWarning("Recovery scan found object {Reference} declared more than once.", obj.Reference);
            }

            _document.ObjectCache.SetObject(obj, objectStart);
            objectsFound++;

            if (obj.Dictionary?.GetName(PdfTokens.TypeKey) == PdfTokens.CatalogKey)
            {
                _document.RootObject = obj;
            }
        }

        _logger.LogInformation("Recovery scanner indexed {Count} object(s).", objectsFound);
    }

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
