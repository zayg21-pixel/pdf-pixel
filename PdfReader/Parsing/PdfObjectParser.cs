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
            if (info.Offset == null)
            {
                return null;
            }

            if (info.Offset.Value > int.MaxValue)
            {
                return null;
            }

            // Use unified PdfParser.ReadObject for indirect object parsing (handles header + value + optional stream).
            var parser = new PdfParser(_document.FileStream, _document, allowReferences: true);
            parser.Position = (int)info.Offset.Value;

            var parsedObject = parser.ReadObject();
            if (parsedObject == null)
            {
                return null;
            }

            // Validate reference matches index metadata to guard against malformed offsets.
            if (parsedObject.Reference.ObjectNumber != info.Reference.ObjectNumber ||
                parsedObject.Reference.Generation != info.Reference.Generation)
            {
                return null;
            }

            return parsedObject;
        }
    }
}