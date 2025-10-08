using System;
using Microsoft.Extensions.Logging;
using PdfReader.Encryption;
using PdfReader.Fonts;
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

        private void ScanObjects(ref PdfParseContext context, string password)
        {
            // TODO: complete as fail case
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
                        _document.StoreParsedObject(pdfObject);
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
        }

        /// <summary>
        /// Lazily parse a single indexed indirect object using only information from the provided PdfObjectInfo.
        /// </summary>
        /// <param name="document">Owning document.</param>
        /// <param name="info">Indexed object metadata.</param>
        /// <returns>Parsed PdfObject or null on failure / unsupported cases.</returns>
        public PdfObject ParseSingleIndexedObject(PdfObjectInfo info)
        {
            if (info == null)
            {
                return null;
            }
            if (info.IsFree)
            {
                return null;
            }
            if (info.IsCompressed)
            {
                return _objectStreamParser.ParseSingleCompressed(info);
            }
            if (info.Offset == null || _document.FileBytes.IsEmpty)
            {
                return null;
            }
            if (info.Offset.Value > int.MaxValue)
            {
                return null;
            }
            var context = new PdfParseContext(_document.FileBytes);
            context.Position = (int)info.Offset.Value;
            if (!PdfParsers.TryParseObjectHeader(ref context, out int objNum, out int gen))
            {
                return null;
            }
            if (objNum != info.Reference.ObjectNumber || gen != info.Reference.Generation)
            {
                return null;
            }
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            var value = PdfParsers.ParsePdfValue(ref context, _document, allowReferences: true);
            var pdfObject = new PdfObject(info.Reference, _document, value);
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Stream))
            {
                pdfObject.StreamData = PdfParsers.ParseStream(ref context, pdfObject.Dictionary);
            }
            return pdfObject;
        }
    }
}