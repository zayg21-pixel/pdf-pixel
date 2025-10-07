using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Parsing;

namespace PdfReader
{
    /// <summary>
    /// Main entry point for reading PDF documents.
    /// Orchestrates the parsing process using specialized parsers.
    /// Supports PDF 1.7 features including cross-reference streams and object streams.
    /// </summary>
    public class PdfDocumentReader
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public PdfDocumentReader(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<PdfDocumentReader>();
        }

        /// <summary>
        /// Read a PDF document from a stream with full PDF 1.7 support.
        /// Returns a document instance that may be partially populated if parsing fails.
        /// </summary>
        /// <param name="stream">Input stream positioned at beginning of a PDF file.</param>
        /// <returns>Parsed <see cref="PdfDocument"/> (partially populated on failure).</returns>
        public PdfDocument Read(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            if (!stream.CanRead)
            {
                throw new InvalidOperationException("Stream must be readable.");
            }
            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Stream must support seeking (required for parsing).");
            }

            var length = stream.Length;
            if (length <= 0)
            {
                _logger.LogWarning("Empty stream encountered when attempting to read PDF.");
                return new PdfDocument(_loggerFactory);
            }

            var buffer = new byte[length];
            stream.Position = 0;
            int readTotal = 0;
            while (readTotal < length)
            {
                int read = stream.Read(buffer, readTotal, (int)(length - readTotal));
                if (read <= 0)
                {
                    break;
                }
                readTotal += read;
            }
            if (readTotal < length)
            {
                _logger.LogWarning("Expected {Expected} bytes, read {Actual} bytes while loading PDF stream.", length, readTotal);
            }

            var context = new PdfParseContext(buffer);
            var document = new PdfDocument(_loggerFactory);
            var resourceLoader = new PdfResourceLoader(document);
            var pageExtractor = new PdfPageExtractor(document);

            try
            {
                var objectParser = new PdfObjectParser(document);
                objectParser.ParseObjects(ref context);

                pageExtractor.ExtractPages();
                resourceLoader.LoadPageResources();

                _logger.LogInformation("Parsed PDF with {PageCount} page(s).", document.PageCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing PDF. Returning partial document where possible.");
            }

            return document;
        }
    }
}
