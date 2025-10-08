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
        private readonly ILogger<PdfObjectParser> _logger;

        public PdfObjectParser(PdfDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _document = document;
            _objectStreamParser = new PdfObjectStreamParser(document);
            _logger = document.LoggerFactory.CreateLogger<PdfObjectParser>();
        }

        /// <summary>
        /// Lazily parse a single indexed indirect object using only information from the provided PdfObjectInfo.
        /// </summary>
        /// <param name="document">Owning document.</param>
        /// <param name="info">Indexed object metadata.</param>
        /// <returns>Parsed PdfObject or null on failure / unsupported cases.</returns>
        public PdfObject ParseSingleIndexedObject(PdfObjectInfo info)
        {
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
            var value = PdfParsers.ParsePdfValue(ref context, _document, info.Reference, allowReferences: true, shouldDecrypt: _document.Decryptor != null);
            var pdfObject = new PdfObject(info.Reference, _document, value);
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Stream))
            {
                var streamData = PdfParsers.ParseStream(ref context, pdfObject.Dictionary);
                pdfObject.StreamData = GetDecryptedStream(streamData, info.Reference);
            }
            return pdfObject;
        }

        private ReadOnlyMemory<byte> GetDecryptedStream(ReadOnlyMemory<byte> streamData, PdfReference reference)
        {
            if (_document.Decryptor != null)
            {
                return _document.Decryptor.DecryptBytes(streamData, reference);
            }

            return streamData;
        }
    }
}