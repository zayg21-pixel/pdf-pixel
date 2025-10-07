using System;
using Microsoft.Extensions.Logging;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles parsing of PDF objects and object streams.
    /// Performs a single forward scan that can also detect trailer dictionaries to set the document RootRef when present.
    /// </summary>
    public class PdfObjectParser
    {
        private readonly PdfDocument _document;
        private readonly PdfObjectStreamParser _objectStreamParser;
        private readonly PdfTrailerParser _pdfTrailerParser;
        private readonly ILogger<PdfObjectParser> _logger;

        public PdfObjectParser(PdfDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _document = document;
            _objectStreamParser = new PdfObjectStreamParser(document);
            _pdfTrailerParser = new PdfTrailerParser(document);
            _logger = document.LoggerFactory.CreateLogger<PdfObjectParser>();
        }

        /// <summary>
        /// Parse all objects in the PDF document (single pass) and extract objects from any object streams.
        /// Also detects trailer dictionaries (by keyword 'trailer') and sets RootRef if not already established.
        /// </summary>
        /// <param name="context">Parse context wrapping the full PDF bytes.</param>
        public void ParseObjects(ref PdfParseContext context)
        {
            context.Position = 0;

            while (context.Position < context.Length - PdfTokens.MinBufferLengthForObjectParsing)
            {
                // Attempt to parse an object header: "number generation obj"
                if (PdfParsers.TryParseObjectHeader(ref context, out int objectNumber, out int generation))
                {
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

                    // Parse the object value (could be any PDF value; dictionary sets obj.Dictionary internally)
                    var value = PdfParsers.ParsePdfValue(ref context, _document, allowReferences: true);
                    var pdfObject = new PdfObject(new PdfReference(objectNumber, generation), _document, value);

                    // If a dictionary precedes a stream keyword, parse the stream data.
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                    if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Stream))
                    {
                        pdfObject.StreamData = PdfParsers.ParseStream(ref context, pdfObject.Dictionary);
                    }

                    if (value != null)
                    {
                        // First write wins; if duplicates appear we could log later (optional enhancement).
                        if (!_document.Objects.ContainsKey(objectNumber))
                        {
                            _document.Objects[objectNumber] = pdfObject;
                        }
                    }

                    if (_document.RootRef == 0)
                    {
                        string typeName = pdfObject.Dictionary.GetName(PdfTokens.TypeKey);

                        if (typeName == PdfTokens.CatalogKey)
                        {
                            _document.RootRef = objectNumber;
                        }
                    }

                    // Advance to endobj (tolerant scan)
                    while (context.Position < context.Length - PdfTokens.MinBufferLengthForEndObj)
                    {
                        if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Endobj))
                        {
                            break;
                        }
                        context.Position++;
                    }
                }
                else if (_pdfTrailerParser.TryParseTrailerDictionary(ref context))
                {
                    // Advance by one byte and continue scanning.
                    context.Position++;
                }
                else
                {
                    context.Position++;
                }
            }

            _pdfTrailerParser.FinalizeTrailer();
            // TODO: we should decrypt object streams with the document decryptor if present.

            DecryptObjects();

            // Extract objects that are embedded in object streams (ObjStm)
            _objectStreamParser.ParseObjectStreams();
        }

        private void DecryptObjects()
        {
            if (_document.Decryptor == null)
            {
                return;
            }

            _document.Decryptor.UpdatePassword("test");

            foreach (var obj in _document.Objects.Values)
            {
                if (!obj.StreamData.IsEmpty)
                {
                    obj.StreamData = _document.Decryptor.DecryptBytes(obj.StreamData, obj.Reference);
                }
            }
        }
    }
}