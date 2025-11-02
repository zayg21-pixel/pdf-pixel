using CommunityToolkit.HighPerformance.Buffers;
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
                return new PdfDocument(_loggerFactory, ReadOnlyMemory<byte>.Empty);
            }

            byte[] buffer = new byte[length];
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

            return Read(buffer, password);
        }

        /// <summary>
        /// Reads a PDF document from the specified data buffer and optionally decrypts it using the provided password.
        /// </summary>
        /// <remarks>This method parses the PDF structure, extracts pages, and processes the first output
        /// intent profile if present. If an error occurs during parsing, a partial <see cref="PdfDocument"/> is
        /// returned, and the error is logged.</remarks>
        /// <param name="data">The PDF file data as a read-only memory buffer. This must contain the full content of the PDF file.</param>
        /// <param name="password">An optional password used to decrypt the PDF if it is encrypted. If the PDF is not encrypted or no password
        /// is required, this parameter can be <see langword="null"/>.</param>
        /// <returns>A <see cref="PdfDocument"/> representing the parsed PDF. The document may be partially populated if an error
        /// occurs during parsing.</returns>
        public PdfDocument Read(ReadOnlyMemory<byte> data, string password = null)
        {
            var context = new PdfParseContext(data);
            var document = new PdfDocument(_loggerFactory, data);
            var xrefLoader = new PdfXrefLoader(document);
            var pageExtractor = new PdfPageExtractor(document);
            var outputIntentParser = new PdfOutputIntentParser(document);

            try
            {
                xrefLoader.LoadXref(ref context);

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
