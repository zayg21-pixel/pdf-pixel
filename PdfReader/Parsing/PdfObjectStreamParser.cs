using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Parsing;

/// <summary>
/// Parser for compressed PDF object streams (ObjStm) introduced in PDF 1.5.
/// Responsible for enumerating object streams and materializing the contained indirect objects.
/// Also provides lazy single-object extraction support used by the general object parser.
/// </summary>
public class PdfObjectStreamParser
{
    private readonly ILogger<PdfObjectStreamParser> _logger;
    private readonly PdfDocument _pdfDocument;

    /// <summary>
    /// Cache of container object number -> decoded bytes so that repeated lazy loads do not re-decode filters.
    /// </summary>
    private readonly Dictionary<uint, ReadOnlyMemory<byte>> _decodedStreamCache = new Dictionary<uint, ReadOnlyMemory<byte>>();

    /// <summary>
    /// Cache of container object number -> mapping of object stream index to relative offset.
    /// Populated when header offsets are first indexed to avoid repeated scans.
    /// </summary>
    private readonly Dictionary<uint, Dictionary<int, int>> _indexToOffsetCache = new Dictionary<uint, Dictionary<int, int>>();

    /// <summary>
    /// Create a new object stream parser bound to a PDF document.
    /// </summary>
    /// <param name="document">Owning <see cref="PdfDocument"/> that provides access to objects and logging.</param>
    public PdfObjectStreamParser(PdfDocument document)
    {
        _pdfDocument = document ?? throw new ArgumentNullException(nameof(document));
        _logger = document.LoggerFactory.CreateLogger<PdfObjectStreamParser>();
    }

    /// <summary>
    /// Lazily parse a single compressed indirect object using its <see cref="PdfObjectInfo"/> metadata.
    /// Populates relative offsets for all objects in the containing object stream on first access.
    /// </summary>
    /// <param name="document">Owning document.</param>
    /// <param name="info">Compressed object index info.</param>
    /// <returns>Materialized <see cref="PdfObject"/> or null if unavailable.</returns>
    public PdfObject ParseSingleCompressed(PdfObjectInfo info)
    {
        var containerReference = new PdfReference(info.ObjectStreamNumber.Value, 0);
        var containerObject = _pdfDocument.ObjectCache.GetObject(containerReference);
        if (containerObject == null || containerObject.Dictionary == null)
        {
            return null;
        }

        if (!_decodedStreamCache.TryGetValue(containerReference.ObjectNumber, out var decoded))
        {
            decoded = containerObject.DecodeAsMemory();
            if (decoded.IsEmpty)
            {
                return null;
            }
            _decodedStreamCache[containerReference.ObjectNumber] = decoded;
        }

        var objectCount = containerObject.Dictionary.GetIntegerOrDefault(PdfTokens.NKey);
        var firstOffset = containerObject.Dictionary.GetIntegerOrDefault(PdfTokens.FirstKey);
        if (objectCount <= 0 || firstOffset < 0)
        {
            return null;
        }

        EnsureOffsetsIndexed(containerReference.ObjectNumber, decoded, objectCount, firstOffset);
        if (info.ObjectStreamRelativeOffset == null)
        {
            return null;
        }

        var span = decoded.Span;
        int objectStart = firstOffset + info.ObjectStreamRelativeOffset.Value;
        if (objectStart < 0 || objectStart >= span.Length)
        {
            return null;
        }

        int objectEnd = span.Length;
        // Find next object's relative offset (same container) with a higher index.
        int targetNextIndex = info.ObjectStreamIndex.Value + 1;
        int? nextRelative = null;

        // Prefer cached lookup to avoid scanning the entire object index.
        if (_indexToOffsetCache.TryGetValue(containerReference.ObjectNumber, out var indexMap))
        {
            if (indexMap.TryGetValue(targetNextIndex, out var offset))
            {
                nextRelative = offset;
            }
        }

        if (nextRelative != null)
        {
            int candidate = firstOffset + nextRelative.Value;
            if (candidate > objectStart && candidate <= span.Length)
            {
                objectEnd = candidate;
            }
        }

        int length = objectEnd - objectStart;
        if (length <= 0)
        {
            return null;
        }

        // Slice directly without copying the entire decoded buffer.
        ReadOnlyMemory<byte> slice = decoded.Slice(objectStart, length);
        var context = new PdfParseContext(slice);
        // Use new PdfParser struct for value parsing (handles whitespace/comments internally).
        var parser = new PdfParser(context, _pdfDocument, allowReferences: true, decrypt: true);
        var value = parser.ReadNextValue();
        if (value == null)
        {
            return null;
        }
        var pdfObject = new PdfObject(info.Reference, _pdfDocument, value);
        return pdfObject;
    }

    private void EnsureOffsetsIndexed(uint containerObjectNumber, ReadOnlyMemory<byte> decoded, int objectCount, int firstOffset)
    {
        // If this container is already cached, no work needed.
        if (_indexToOffsetCache.ContainsKey(containerObjectNumber))
        {
            return;
        }

        if (firstOffset > decoded.Length)
        {
            return;
        }

        // Header slice without copying.
        ReadOnlyMemory<byte> headerMemory = decoded.Slice(0, firstOffset);
        var headerContext = new PdfParseContext(headerMemory);
        // Unified parsing via PdfParser for header: sequence of objectNumber relativeOffset pairs.
        var headerParser = new PdfParser(headerContext, _pdfDocument, allowReferences: false, decrypt: false);

        // Prepare cache for this container.
        var indexMap = new Dictionary<int, int>(capacity: objectCount);
        _indexToOffsetCache[containerObjectNumber] = indexMap;

        for (int index = 0; index < objectCount; index++)
        {
            var objectNumberValue = headerParser.ReadNextValue();
            if (objectNumberValue == null || objectNumberValue.Type != PdfValueType.Integer)
            {
                break;
            }
            var offsetValue = headerParser.ReadNextValue();
            if (offsetValue == null || offsetValue.Type != PdfValueType.Integer)
            {
                break;
            }

            uint objectNumber = (uint)objectNumberValue.AsInteger();
            int relativeOffset = offsetValue.AsInteger();

            // Cache the index -> offset mapping for fast lookup.
            if (!indexMap.ContainsKey(index))
            {
                indexMap[index] = relativeOffset;
            }

            var reference = new PdfReference(objectNumber, 0);
            if (_pdfDocument.ObjectCache.ObjectIndex.TryGetValue(reference, out var info))
            {
                if (info.IsCompressed && info.ObjectStreamNumber == containerObjectNumber)
                {
                    info.ObjectStreamRelativeOffset = relativeOffset;
                }
            }
        }
    }
}