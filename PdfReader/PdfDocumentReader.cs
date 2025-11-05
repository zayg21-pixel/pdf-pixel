using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Parsing;
using System;
using System.IO;

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
        /// Reads a PDF document from the specified stream, optionally using a password for decryption.
        /// </summary>
        /// <remarks>The method reads the entire content of the stream into memory. If the stream contains
        /// fewer bytes than expected, a warning is logged, and the method attempts to process the available
        /// data.</remarks>
        /// <param name="stream">The input <see cref="Stream"/> containing the PDF data. The stream must be readable and seekable.</param>
        /// <param name="password">An optional password used to decrypt the PDF, if it is encrypted. If the PDF is not encrypted, this
        /// parameter can be <see langword="null"/>.</param>
        /// <returns>A <see cref="PdfDocument"/> representing the parsed PDF content. If the stream is empty, an empty <see
        /// cref="PdfDocument"/> is returned.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="stream"/> is not readable or does not support seeking.</exception>
        public PdfDocument Read(Stream stream, string password = null)
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

            long length = stream.Length;
            if (length <= 0)
            {
                _logger.LogWarning("Empty stream encountered when attempting to read PDF.");
                return new PdfDocument(_loggerFactory, stream);
            }

            var document = new PdfDocument(_loggerFactory, stream);
            var xrefLoader = new PdfXrefLoader(document);
            var pageExtractor = new PdfPageExtractor(document);
            var outputIntentParser = new PdfOutputIntentParser(document);

            try
            {
                xrefLoader.LoadXref();

                document.Decryptor?.UpdatePassword(password);

                pageExtractor.ExtractPages();
                outputIntentParser.ParseFirstOutputIntentProfile();

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
