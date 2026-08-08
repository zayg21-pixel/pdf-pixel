using System;
using System.Collections.Generic;
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
        _document.ObjectCache.RecoveryScanAttempted = true;

        RecoverDecryptor();

        PdfParser parser = new(_document.Stream, _document, allowReferences: true, decrypt: true);
        parser.Position = 0;
        int objectsFound = 0;
        List<PdfObject> objectStreams = [];

        while (!parser.IsAtEnd)
        {
            int objectStart = parser.Position;
            PdfObject? obj = parser.ScanObject();

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

            PdfString typeName = obj.Dictionary.GetName(PdfTokens.TypeKey);

            if (typeName == PdfTokens.CatalogKey)
            {
                _document.RootObject = obj;
            }
            else if (typeName == PdfTokens.ObjStmKey)
            {
                objectStreams.Add(obj);
            }
        }

        int compressedFound = 0;

        foreach (PdfObject objectStream in objectStreams)
        {
            compressedFound += IndexObjectStreamContents(objectStream);
        }

        _logger.LogInformation(
            "Recovery scanner indexed {Count} object(s), and {CompressedCount} more held by {StreamCount} object stream(s).",
            objectsFound,
            compressedFound,
            objectStreams.Count);
    }

    /// <summary>
    /// Registers the objects held by a compressed object stream, which carry no <c>N G obj</c>
    /// declaration of their own and so are invisible to the scan. The container lists every object it
    /// holds in its own header, as an object number and an offset relative to the container, so its
    /// contents are recoverable even when no file offset in the document can be trusted.
    /// </summary>
    /// <param name="containerObject">An object the scan found to be of <c>/Type /ObjStm</c>.</param>
    /// <returns>The number of contained objects added to the index.</returns>
    private int IndexObjectStreamContents(PdfObject containerObject)
    {
        int objectCount = containerObject.Dictionary.GetIntegerOrDefault(PdfTokens.NKey);
        int firstOffset = containerObject.Dictionary.GetIntegerOrDefault(PdfTokens.FirstKey);

        if (objectCount <= 0 || firstOffset <= 0)
        {
            return 0;
        }

        ReadOnlyMemory<byte> decoded = containerObject.DecodeAsMemory();

        if (decoded.Length < firstOffset)
        {
            _logger.LogWarning("Object stream {Reference} decoded to {Length} byte(s), too short for its {First} byte header.", containerObject.Reference, decoded.Length, firstOffset);
            return 0;
        }

        PdfParseContext headerContext = new(decoded.Slice(0, firstOffset));
        PdfParser headerParser = new(headerContext, _document, allowReferences: false, decrypt: false);
        int registeredCount = 0;

        for (int index = 0; index < objectCount; index++)
        {
            IPdfValue? objectNumberValue = headerParser.ReadNextValue();
            IPdfValue? offsetValue = headerParser.ReadNextValue();

            if (objectNumberValue == null
                || objectNumberValue.Type != PdfValueType.Integer
                || offsetValue == null
                || offsetValue.Type != PdfValueType.Integer)
            {
                break;
            }

            PdfReference reference = new((uint)objectNumberValue.AsInteger(), 0);

            // An object declared directly in the file was located by the scan itself, at a position
            // that is known to be right, and so outranks the copy held in the object stream.
            if (!reference.IsValid || _document.ObjectCache.ObjectIndex.ContainsKey(reference))
            {
                continue;
            }

            _document.ObjectCache.ObjectIndex[reference] = PdfObjectInfo.ForCompressed(
                reference,
                containerObject.Reference.ObjectNumber,
                index,
                fromXrefStream: false);

            registeredCount++;
        }

        return registeredCount;
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
