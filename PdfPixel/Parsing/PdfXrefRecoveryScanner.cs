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

    public PdfXrefRecoveryScanner(IPdfDocumentInternal document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = document.LoggerFactory.CreateLogger<PdfXrefRecoveryScanner>();
    }

    public void Scan()
    {
        PdfParser parser = new(_document.Stream, _document, allowReferences: true, decrypt: false);
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

            PdfReference reference = obj.Reference;
            PdfObjectInfo info = PdfObjectInfo.ForUncompressed(reference, objectStart, false);
            info.FromFallbackScan = true;
            _document.ObjectCache.ObjectIndex[reference] = info;
            objectsFound++;

            if (_document.RootObject == null
                && obj.Dictionary?.GetName(PdfTokens.TypeKey) == PdfTokens.CatalogKey)
            {
                _document.RootObject = obj;
            }
        }

        _logger.LogInformation("Recovery scanner indexed {Count} object(s).", objectsFound);
    }
}
