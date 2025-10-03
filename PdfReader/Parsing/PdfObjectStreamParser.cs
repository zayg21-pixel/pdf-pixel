using System;
using System.Collections.Generic;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Enhanced object stream parser for PDF 1.5+ compressed objects
    /// Handles extraction of multiple objects from compressed object streams
    /// </summary>
    public static class PdfObjectStreamParser
    {
        /// <summary>
        /// Extract objects from a single object stream (ObjStm)
        /// Object streams contain multiple objects in a compressed format
        /// </summary>
        public static int ExtractObjectsFromSingleStream(PdfDocument document, PdfObject objStream)
        {
            if (objStream?.StreamData.IsEmpty != false)
                return 0;

            var dict = objStream.Dictionary;
            if (dict == null)
                return 0;

            // Get the number of objects in the stream
            int n = dict.GetInteger(PdfTokens.NKey);
            if (n <= 0)
            {
                Console.WriteLine($"Invalid /N value in object stream {objStream.Reference.ObjectNumber}: {n}");
                return 0;
            }

            // Get the offset to the first object
            int first = dict.GetInteger(PdfTokens.FirstKey);
            if (first < 0)
            {
                Console.WriteLine($"Invalid /First value in object stream {objStream.Reference.ObjectNumber}: {first}");
                return 0;
            }

            // Decode the stream
            var decodedData = PdfStreamDecoder.DecodeContentStream(objStream);
            if (decodedData.IsEmpty)
            {
                Console.WriteLine($"Failed to decode object stream {objStream.Reference.ObjectNumber}");
                return 0;
            }

            try
            {
                return ParseCompressedObjects(document, decodedData, n, first, objStream.Reference.ObjectNumber);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing compressed objects from stream {objStream.Reference.ObjectNumber}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Parse the compressed objects from decoded stream data
        /// </summary>
        private static int ParseCompressedObjects(PdfDocument document, ReadOnlyMemory<byte> decodedData, 
            int numObjects, int firstOffset, int streamObjectNumber)
        {
            var context = new PdfParseContext(decodedData);
            var objectOffsets = new List<(int objNum, int offset)>();

            // Parse the offset table at the beginning of the stream
            for (int i = 0; i < numObjects; i++)
            {
                PdfHelpers.SkipWhitespaceAndComment(ref context);
                
                if (!PdfParsers.TryParseNumber(ref context, out int objNum))
                {
                    Console.WriteLine($"Failed to parse object number {i} in stream {streamObjectNumber}");
                    break;
                }

                PdfHelpers.SkipWhitespaceAndComment(ref context);
                
                if (!PdfParsers.TryParseNumber(ref context, out int offset))
                {
                    Console.WriteLine($"Failed to parse offset for object {objNum} in stream {streamObjectNumber}");
                    break;
                }

                objectOffsets.Add((objNum, offset));
            }

            int extractedCount = 0;

            // Extract each object
            for (int i = 0; i < objectOffsets.Count; i++)
            {
                var (objNum, relativeOffset) = objectOffsets[i];
                int absoluteOffset = firstOffset + relativeOffset;

                if (absoluteOffset >= decodedData.Length)
                {
                    Console.WriteLine($"Invalid offset {absoluteOffset} for object {objNum} in stream {streamObjectNumber}");
                    continue;
                }

                // Determine the end position for this object
                int endOffset = (i + 1 < objectOffsets.Count) 
                    ? firstOffset + objectOffsets[i + 1].offset
                    : decodedData.Length;

                try
                {
                    var extractedObj = ExtractSingleObjectFromStream(document, decodedData, 
                        objNum, absoluteOffset, endOffset, streamObjectNumber);
                    
                    if (extractedObj != null)
                    {
                        document.Objects[objNum] = extractedObj;
                        extractedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting object {objNum} from stream {streamObjectNumber}: {ex.Message}");
                }
            }

            return extractedCount;
        }

        /// <summary>
        /// Extract a single object from the decoded stream data
        /// </summary>
        private static PdfObject ExtractSingleObjectFromStream(PdfDocument document, ReadOnlyMemory<byte> decodedData,
            int objNum, int startOffset, int endOffset, int streamObjectNumber)
        {
            if (startOffset >= endOffset || startOffset >= decodedData.Length)
                return null;

            // Create a sub-context for this object
            var objectData = decodedData.Slice(startOffset, Math.Min(endOffset - startOffset, decodedData.Length - startOffset));
            var context = new PdfParseContext(objectData);

            PdfHelpers.SkipWhitespaceAndComment(ref context);

            // Parse the object's value
            var value = PdfParsers.ParsePdfValue(ref context, document);

            // Create the object (generation is always 0 for compressed objects)
            var obj = new PdfObject(new PdfReference(objNum, 0), document, value);

            return obj;
        }

        /// <summary>
        /// Check if an object is a valid object stream
        /// </summary>
        public static bool IsObjectStream(PdfObject obj)
        {
            return obj?.Dictionary?.GetName(PdfTokens.TypeKey) == PdfTokens.ObjStmKey;
        }

        /// <summary>
        /// Get information about an object stream
        /// </summary>
        public static (int numObjects, int firstOffset) GetObjectStreamInfo(PdfObject objStream)
        {
            if (!IsObjectStream(objStream))
                return (0, 0);

            var dict = objStream.Dictionary;
            int n = dict.GetInteger(PdfTokens.NKey);
            int first = dict.GetInteger(PdfTokens.FirstKey);

            return (n, first);
        }

        /// <summary>
        /// Validate object stream structure
        /// </summary>
        public static bool ValidateObjectStream(PdfObject objStream)
        {
            if (!IsObjectStream(objStream))
                return false;

            var (numObjects, firstOffset) = GetObjectStreamInfo(objStream);
            
            if (numObjects <= 0)
            {
                Console.WriteLine($"Invalid object count in stream {objStream.Reference.ObjectNumber}: {numObjects}");
                return false;
            }

            if (firstOffset < 0)
            {
                Console.WriteLine($"Invalid first offset in stream {objStream.Reference.ObjectNumber}: {firstOffset}");
                return false;
            }

            if (objStream.StreamData.IsEmpty)
            {
                Console.WriteLine($"Object stream {objStream.Reference.ObjectNumber} has no stream data");
                return false;
            }

            return true;
        }
    }
}