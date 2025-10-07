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
    /// </summary>
    public class PdfObjectStreamParser
    {
        private readonly ILogger<PdfObjectStreamParser> _logger;
        private readonly PdfDocument _pdfDocument;

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
        /// Iterate existing document objects and extract any compressed object streams (objects with /Type /ObjStm).
        /// </summary>
        public void ParseObjectStreams()
        {
            var objectsSnapshot = _pdfDocument.Objects.Values.ToArray();

            foreach (var pdfObject in objectsSnapshot)
            {
                if (pdfObject.Dictionary.GetName(PdfTokens.TypeKey) == PdfTokens.ObjStmKey)
                {
                    ExtractObjects(pdfObject);
                }
            }
        }

        /// <summary>
        /// Extract all embedded indirect objects from a single object stream (ObjStm) container.
        /// </summary>
        /// <param name="pdfObject">The object stream container object.</param>
        public void ExtractObjects(PdfObject pdfObject)
        {
            if (pdfObject.StreamData.IsEmpty != false)
            {
                return;
            }

            var dictionary = pdfObject.Dictionary;

            int objectCount = dictionary.GetIntegerOrDefault(PdfTokens.NKey);
            if (objectCount <= 0)
            {
                _logger.LogWarning("Invalid /N value in object stream {ObjectNumber}: {Value}", pdfObject.Reference.ObjectNumber, objectCount);
                return;
            }

            int firstObjectDataOffset = dictionary.GetIntegerOrDefault(PdfTokens.FirstKey);
            if (firstObjectDataOffset < 0)
            {
                _logger.LogWarning("Invalid /First value in object stream {ObjectNumber}: {Value}", pdfObject.Reference.ObjectNumber, firstObjectDataOffset);
                return;
            }

            var decodedData = PdfStreamDecoder.DecodeContentStream(pdfObject);
            if (decodedData.IsEmpty)
            {
                _logger.LogWarning("Failed to decode object stream {ObjectNumber}", pdfObject.Reference.ObjectNumber);
                return;
            }

            try
            {
                ParseCompressedObjects(decodedData, objectCount, firstObjectDataOffset, pdfObject.Reference.ObjectNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing compressed objects from stream {ObjectNumber}", pdfObject.Reference.ObjectNumber);
            }
        }

        /// <summary>
        /// Parse the compressed indirect objects contained in an already decoded object stream.
        /// </summary>
        /// <param name="decodedData">Decoded (unfiltered) object stream bytes.</param>
        /// <param name="numObjects">Number of contained indirect objects (/N).</param>
        /// <param name="firstOffset">Byte offset to the first object data segment (/First).</param>
        /// <param name="streamObjectNumber">Object number of the owning ObjStm container (for diagnostics).</param>
        /// <returns>The number of extracted objects successfully materialized.</returns>
        private int ParseCompressedObjects(ReadOnlyMemory<byte> decodedData,
            int numObjects, int firstOffset, int streamObjectNumber)
        {
            var parseContext = new PdfParseContext(decodedData);
            var objectOffsets = new List<(int objectNumber, int relativeOffset)>();

            // Parse the offset table at the beginning of the stream
            for (int index = 0; index < numObjects; index++)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref parseContext);

                if (!PdfParsers.TryParseNumber(ref parseContext, out int objectNumber))
                {
                    _logger.LogWarning("Failed to parse object number {Index} in stream {StreamObjectNumber}", index, streamObjectNumber);
                    break;
                }

                PdfParsingHelpers.SkipWhitespaceAndComment(ref parseContext);

                if (!PdfParsers.TryParseNumber(ref parseContext, out int relativeOffset))
                {
                    _logger.LogWarning("Failed to parse offset for object {ObjectNumber} in stream {StreamObjectNumber}", objectNumber, streamObjectNumber);
                    break;
                }

                objectOffsets.Add((objectNumber, relativeOffset));
            }

            int extractedCount = 0;

            // Extract each object
            for (int index = 0; index < objectOffsets.Count; index++)
            {
                var (objectNumber, relativeOffset) = objectOffsets[index];
                int absoluteOffset = firstOffset + relativeOffset;

                if (absoluteOffset >= decodedData.Length)
                {
                    _logger.LogWarning("Invalid offset {AbsoluteOffset} for object {ObjectNumber} in stream {StreamObjectNumber}", absoluteOffset, objectNumber, streamObjectNumber);
                    continue;
                }

                // Determine the end position for this object
                int endOffset = (index + 1 < objectOffsets.Count)
                    ? firstOffset + objectOffsets[index + 1].relativeOffset
                    : decodedData.Length;

                try
                {
                    var extractedObject = ExtractSingleObjectFromStream(decodedData, objectNumber, absoluteOffset, endOffset);

                    if (extractedObject != null)
                    {
                        _pdfDocument.Objects[objectNumber] = extractedObject;
                        extractedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error extracting object {ObjectNumber} from stream {StreamObjectNumber}", objectNumber, streamObjectNumber);
                }
            }

            return extractedCount;
        }

        /// <summary>
        /// Extract a single indirect object value from a segment of the decoded object stream.
        /// </summary>
        /// <param name="decodedData">Full decoded object stream bytes.</param>
        /// <param name="objNum">Object number for the indirect object being extracted.</param>
        /// <param name="startOffset">Inclusive start offset of the object's data relative to the stream start.</param>
        /// <param name="endOffset">Exclusive end offset delimiting this object's data slice.</param>
        /// <returns>The constructed <see cref="PdfObject"/> or null if offset bounds are invalid.</returns>
        private PdfObject ExtractSingleObjectFromStream(ReadOnlyMemory<byte> decodedData,
            int objNum, int startOffset, int endOffset)
        {
            if (startOffset >= endOffset || startOffset >= decodedData.Length)
            {
                return null;
            }

            // Create a sub-context for this object
            var objectData = decodedData.Slice(startOffset, Math.Min(endOffset - startOffset, decodedData.Length - startOffset));
            var objectParseContext = new PdfParseContext(objectData);

            PdfParsingHelpers.SkipWhitespaceAndComment(ref objectParseContext);

            // Parse the object's value
            var value = PdfParsers.ParsePdfValue(ref objectParseContext, _pdfDocument, allowReferences: true);

            // Create the object (generation is always 0 for compressed objects)
            var pdfObject = new PdfObject(new PdfReference(objNum, 0), _pdfDocument, value);

            return pdfObject;
        }
    }
}