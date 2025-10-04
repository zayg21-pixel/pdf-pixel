using System;
using System.Collections.Generic;
using System.IO;
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

            try
            {
                var versionInfo = PdfVersionParser.ParsePdfVersion(ref context);
                bool isValidVersion = versionInfo.isValid;
                string version = versionInfo.version;
                bool requiresAdvancedFeatures = versionInfo.requiresAdvancedFeatures;

                if (!isValidVersion)
                {
                    _logger.LogWarning("Invalid or unsupported PDF version '{Version}'. Continuing optimistically.", version);
                }
                else
                {
                    _logger.LogInformation("Detected PDF version {Version}.", version);
                    if (requiresAdvancedFeatures)
                    {
                        _logger.LogInformation("Advanced PDF features required (version {Version}). Using enhanced parsing path.", version);
                        var features = PdfVersionParser.GetRequiredFeatures(version);
                        _logger.LogDebug("Declared advanced feature flags: {Features}.", features);
                    }
                }

                int xrefPosition = PdfXrefParser.FindStartXref(ref context);
                if (xrefPosition >= 0)
                {
                    bool parsedViaStream = false;
                    if (requiresAdvancedFeatures && PdfXrefStreamParser.IsXrefStream(ref context, xrefPosition))
                    {
                        _logger.LogInformation("Cross-reference stream detected at position {Pos}.", xrefPosition);
                        PdfXrefStreamParser.ParseXrefStream(ref context, document, xrefPosition);
                        parsedViaStream = true;
                    }
                    if (!parsedViaStream)
                    {
                        _logger.LogInformation("Traditional cross-reference table detected at position {Pos}.", xrefPosition);
                        PdfXrefParser.ParseXrefAndTrailer(ref context, document, xrefPosition);
                    }
                }
                else
                {
                    _logger.LogWarning("No cross-reference table offset found (startxref missing).");
                }

                PdfObjectParser.ParseObjects(ref context, document);

                if (versionInfo.requiresAdvancedFeatures)
                {
                    ProcessObjectStreams(document);
                }

                PdfPageExtractor.ExtractPages(document);
                PdfResourceLoader.LoadPageResources(document);

                _logger.LogInformation("Parsed PDF {Version} with {PageCount} page(s).", versionInfo.version, document.PageCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing PDF. Returning partial document where possible.");
            }

            return document;
        }

        /// <summary>
        /// Process object streams and extract their embedded objects (PDF 1.5+ feature).
        /// </summary>
        /// <param name="document">Target PDF document.</param>
        private void ProcessObjectStreams(PdfDocument document)
        {
            if (document == null)
            {
                return;
            }

            var objectStreams = new List<PdfObject>();
            foreach (var pdfObject in document.Objects.Values)
            {
                if (PdfObjectStreamParser.IsObjectStream(pdfObject))
                {
                    objectStreams.Add(pdfObject);
                }
            }

            if (objectStreams.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Found {Count} object stream(s). Extracting compressed objects.", objectStreams.Count);

            int totalExtracted = 0;
            foreach (var objStream in objectStreams)
            {
                if (PdfObjectStreamParser.ValidateObjectStream(objStream))
                {
                    int extracted = PdfObjectStreamParser.ExtractObjectsFromSingleStream(document, objStream);
                    totalExtracted += extracted;
                }
                else
                {
                    _logger.LogWarning("Invalid object stream skipped (obj {ObjNumber}).", objStream.Reference.ObjectNumber);
                }
            }

            _logger.LogInformation("Extracted {Total} object(s) from object stream(s).", totalExtracted);
        }
    }
}
