using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Streams;

namespace PdfReader.Parsing
{
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
        private readonly Dictionary<int, ReadOnlyMemory<byte>> _decodedStreamCache = new Dictionary<int, ReadOnlyMemory<byte>>();

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
            var containerObject = _pdfDocument.GetObject(containerReference);
            if (containerObject == null || containerObject.Dictionary == null)
            {
                return null;
            }

            if (!_decodedStreamCache.TryGetValue(containerReference.ObjectNumber, out var decoded))
            {
                decoded = _pdfDocument.StreamDecoder.DecodeContentStream(containerObject);
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
            int? nextRelative = FindRelativeOffset(containerReference.ObjectNumber, targetNextIndex);
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

            var slice = new ReadOnlyMemory<byte>(decoded.ToArray(), objectStart, length);
            var context = new PdfParseContext(slice);
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            var value = PdfParsers.ParsePdfValue(ref context, _pdfDocument, allowReferences: true);
            if (value == null)
            {
                return null;
            }
            var pdfObject = new PdfObject(info.Reference, _pdfDocument, value);
            return pdfObject;
        }

        private void EnsureOffsetsIndexed(int containerObjectNumber, ReadOnlyMemory<byte> decoded, int objectCount, int firstOffset)
        {
            // If at least one compressed object for this container already has relative offset populated, assume done.
            foreach (var kvp in _pdfDocument.ObjectIndex)
            {
                var entry = kvp.Value;
                if (entry.IsCompressed && entry.ObjectStreamNumber == containerObjectNumber && entry.ObjectStreamRelativeOffset != null)
                {
                    return;
                }
            }

            if (firstOffset > decoded.Length)
            {
                return;
            }

            var headerMemory = new ReadOnlyMemory<byte>(decoded.ToArray(), 0, firstOffset);
            var headerContext = new PdfParseContext(headerMemory);

            for (int index = 0; index < objectCount; index++)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref headerContext);
                if (!PdfParsers.TryParseNumber(ref headerContext, out int objectNumber))
                {
                    break;
                }
                PdfParsingHelpers.SkipWhitespaceAndComment(ref headerContext);
                if (!PdfParsers.TryParseNumber(ref headerContext, out int relativeOffset))
                {
                    break;
                }
                var reference = new PdfReference(objectNumber, 0);
                if (_pdfDocument.ObjectIndex.TryGetValue(reference, out var info))
                {
                    if (info.IsCompressed && info.ObjectStreamNumber == containerObjectNumber)
                    {
                        info.ObjectStreamRelativeOffset = relativeOffset;
                    }
                }
            }
        }

        private int? FindRelativeOffset(int containerObjectNumber, int targetIndex)
        {
            foreach (var kvp in _pdfDocument.ObjectIndex)
            {
                var entry = kvp.Value;
                if (entry.IsCompressed && entry.ObjectStreamNumber == containerObjectNumber && entry.ObjectStreamIndex == targetIndex)
                {
                    return entry.ObjectStreamRelativeOffset;
                }
            }
            return null;
        }
    }
}