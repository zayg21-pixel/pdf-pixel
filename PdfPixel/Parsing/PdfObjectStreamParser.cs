using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Parser for compressed PDF object streams (ObjStm) introduced in PDF 1.5.
/// Responsible for enumerating object streams and materializing the contained indirect objects.
/// Also provides lazy single-object extraction support used by the general object parser.
/// </summary>
internal class PdfObjectStreamParser
{
    private readonly ILogger<PdfObjectStreamParser> _logger;
    private readonly IPdfDocumentInternal _pdfDocument;

    /// <summary>
    /// Cache of container object number -> everything a compressed object needs from its container.
    /// The container object is read once to build the entry and is not held afterwards.
    /// </summary>
    private readonly Dictionary<uint, ObjectStreamContent> _containers = [];

    /// <summary>
    /// Create a new object stream parser bound to a PDF document.
    /// </summary>
    /// <param name="document">Owning <see cref="PdfDocument"/> that provides access to objects and logging.</param>
    public PdfObjectStreamParser(IPdfDocumentInternal document)
    {
        _pdfDocument = document ?? throw new ArgumentNullException(nameof(document));
        _logger = document.LoggerFactory.CreateLogger<PdfObjectStreamParser>();
    }

    /// <summary>
    /// Lazily parse a single compressed indirect object using its <see cref="PdfObjectInfo"/> metadata.
    /// Populates relative offsets for all objects in the containing object stream on first access.
    /// </summary>
    /// <param name="reference">Reference the index holds this entry under.</param>
    /// <param name="info">Compressed object index info.</param>
    /// <returns>Materialized <see cref="PdfObject"/> or null if unavailable.</returns>
    public PdfObject? ParseSingleCompressed(in PdfReference reference, PdfObjectInfo info)
    {
        if (info.ObjectStreamNumber == null || info.ObjectStreamIndex == null)
        {
            return null;
        }

        uint containerObjectNumber = info.ObjectStreamNumber.Value;

        if (!_containers.TryGetValue(containerObjectNumber, out ObjectStreamContent? container))
        {
            container = LoadContainer(containerObjectNumber);
            if (container == null)
            {
                return null;
            }

            _containers[containerObjectNumber] = container;
        }

        if (info.ObjectStreamRelativeOffset == null)
        {
            return null;
        }

        ReadOnlySpan<byte> span = container.Decoded.Span;
        int objectStart = container.FirstOffset + info.ObjectStreamRelativeOffset.Value;
        if (objectStart < 0 || objectStart >= span.Length)
        {
            return null;
        }

        int objectEnd = span.Length;
        // Find next object's relative offset (same container) with a higher index.
        int targetNextIndex = info.ObjectStreamIndex.Value + 1;

        if (targetNextIndex < container.RelativeOffsets.Length)
        {
            int candidate = container.FirstOffset + container.RelativeOffsets[targetNextIndex];
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
        ReadOnlyMemory<byte> slice = container.Decoded.Slice(objectStart, length);
        PdfParseContext context = new(slice);
        // Use new PdfParser struct for value parsing (handles whitespace/comments internally).
        PdfParser parser = new(context, _pdfDocument, allowReferences: true, decrypt: true);
        IPdfValue? value = parser.ReadNextValue();
        if (value == null)
        {
            return null;
        }

        PdfObject pdfObject = new(reference, _pdfDocument, value);
        return pdfObject;
    }

    /// <summary>
    /// Reads a container object stream once: decodes it, takes the object count and first offset from
    /// its dictionary, and indexes the header's relative offsets. Returns null when the container is
    /// missing or malformed.
    /// </summary>
    private ObjectStreamContent? LoadContainer(uint containerObjectNumber)
    {
        PdfReference containerReference = new(containerObjectNumber, 0);
        PdfObject? containerObject = _pdfDocument.ObjectCache.GetObject(containerReference);
        if (containerObject == null || containerObject.Dictionary == null)
        {
            return null;
        }

        ReadOnlyMemory<byte> decoded = containerObject.DecodeAsMemory();
        if (decoded.IsEmpty)
        {
            return null;
        }

        int objectCount = containerObject.Dictionary.GetIntegerOrDefault(PdfTokens.NKey);
        int firstOffset = containerObject.Dictionary.GetIntegerOrDefault(PdfTokens.FirstKey);
        if (objectCount <= 0 || firstOffset < 0 || firstOffset > decoded.Length)
        {
            return null;
        }

        // Header slice without copying.
        ReadOnlyMemory<byte> headerMemory = decoded.Slice(0, firstOffset);
        PdfParseContext headerContext = new(headerMemory);
        // Unified parsing via PdfParser for header: sequence of objectNumber relativeOffset pairs.
        PdfParser headerParser = new(headerContext, _pdfDocument, allowReferences: false, decrypt: false);

        var relativeOffsets = new int[objectCount];
        int parsedCount = 0;

        for (int index = 0; index < objectCount; index++)
        {
            int? objectNumberValue = headerParser.ReadNextValue().AsInteger();
            if (objectNumberValue == null)
            {
                break;
            }

            int? offsetValue = headerParser.ReadNextValue().AsInteger();
            if (offsetValue == null)
            {
                break;
            }

            relativeOffsets[index] = offsetValue.Value;
            parsedCount = index + 1;

            PdfReference reference = new((uint)objectNumberValue.Value, 0);
            if (_pdfDocument.ObjectCache.ObjectIndex.TryGetValue(reference, out PdfObjectInfo? info))
            {
                if (info.IsCompressed && info.ObjectStreamNumber == containerObjectNumber)
                {
                    info.ObjectStreamRelativeOffset = offsetValue.Value;
                }
            }
        }

        // A header that stopped short leaves a shorter array, so its length is what says which indexes
        // are present rather than a placeholder offset standing in for a missing one.
        if (parsedCount < objectCount)
        {
            Array.Resize(ref relativeOffsets, parsedCount);
        }

        return new ObjectStreamContent(decoded, firstOffset, relativeOffsets);
    }

    /// <summary>
    /// What a compressed object needs from the object stream containing it.
    /// </summary>
    private sealed class ObjectStreamContent
    {
        public ObjectStreamContent(in ReadOnlyMemory<byte> decoded, int firstOffset, int[] relativeOffsets)
        {
            Decoded = decoded;
            FirstOffset = firstOffset;
            RelativeOffsets = relativeOffsets;
        }

        /// <summary>
        /// Decoded container stream bytes.
        /// </summary>
        public ReadOnlyMemory<byte> Decoded { get; }

        /// <summary>
        /// Byte offset of the first contained object, from the container's /First entry.
        /// </summary>
        public int FirstOffset { get; }

        /// <summary>
        /// Relative offset of each contained object, indexed by its position in the container.
        /// </summary>
        public int[] RelativeOffsets { get; }
    }
}
