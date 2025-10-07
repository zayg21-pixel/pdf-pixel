using System;
using System.Collections.Generic;
using System.Linq;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles parsing of PDF objects and object streams
    /// </summary>
    public class PdfObjectParser
    {
        private readonly PdfDocument _document;
        private readonly PdfObjectStreamParser _objectStreamParser;

        public PdfObjectParser(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _objectStreamParser = new PdfObjectStreamParser(document);
        }

        /// <summary>
        /// Parse all objects in the PDF document
        /// </summary>
        public void ParseObjects(ref PdfParseContext context)
        {
            context.Position = 0;
            
            while (context.Position < context.Length - PdfTokens.MinBufferLengthForObjectParsing)
            {
                // Look for object start pattern: "number generation obj"
                if (PdfParsers.TryParseObjectHeader(ref context, out int objNum, out int generation))
                {
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

                    // Parse any value as DirectValue; if it is a dictionary, also set Dictionary
                    var value = PdfParsers.ParsePdfValue(ref context, _document, allowReferences: true);

                    var obj = new PdfObject(new PdfReference(objNum, generation), _document, value);

                    // Parse stream only when a dictionary precedes it
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                    if (obj.Dictionary != null && PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Stream))
                    {
                        obj.StreamData = PdfParsers.ParseStream(ref context, obj.Dictionary);
                    }

                    if (value != null)
                    {
                        _document.Objects[objNum] = obj;
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
            _objectStreamParser.ParseObjectStreams();
        }
    }
}