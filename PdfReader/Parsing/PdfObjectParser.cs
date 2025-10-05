using System;
using System.Collections.Generic;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles parsing of PDF objects and object streams
    /// </summary>
    public static class PdfObjectParser
    {
        /// <summary>
        /// Parse all objects in the PDF document
        /// </summary>
        public static void ParseObjects(ref PdfParseContext context, PdfDocument document)
        {
            context.Position = 0;
            var objectStreams = new List<PdfObject>();
            
            while (context.Position < context.Length - PdfTokens.MinBufferLengthForObjectParsing)
            {
                // Look for object start pattern: "number generation obj"
                if (PdfParsers.TryParseObjectHeader(ref context, out int objNum, out int generation))
                {
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

                    // Parse any value as DirectValue; if it is a dictionary, also set Dictionary
                    var value = PdfParsers.ParsePdfValue(ref context, document, allowReferences: true);

                    var obj = new PdfObject(new PdfReference(objNum, generation), document, value);

                    // Parse stream only when a dictionary precedes it
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                    if (obj.Dictionary != null && PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Stream))
                    {
                        obj.StreamData = PdfParsers.ParseStream(ref context, obj.Dictionary);
                    }

                    if (value != null)
                    {
                        document.Objects[objNum] = obj;
                    }

                    // Skip to endobj - with bounds checking
                    int searchCount = 0;
                    while (context.Position < context.Length - PdfTokens.MinBufferLengthForEndObj)
                    {
                        if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Endobj))
                        {
                            break;
                        }
                        context.Position++;
                        searchCount++;
                    }
                }
                else
                {
                    context.Position++;
                }
            }
            

            // Extract objects from object streams
            ExtractObjectsFromStreams(document, objectStreams);
        }
        
        /// <summary>
        /// Extract objects from object streams
        /// </summary>
        public static int ExtractObjectsFromStreams(PdfDocument document, List<PdfObject> objectStreams)
        {
            int extractedCount = 0;
            
            foreach (var objStream in objectStreams)
            {
                try
                {
                    extractedCount += PdfObjectStreamParser.ExtractObjectsFromSingleStream(document, objStream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting objects from stream {objStream.Reference.ObjectNumber}: {ex.Message}");
                }
            }
            
            return extractedCount;
        }
    }
}