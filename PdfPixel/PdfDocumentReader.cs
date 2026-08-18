using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Fonts.Management;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.TextExtraction;
using System;
using System.IO;

namespace PdfPixel;

/// <summary>
/// Entry point for opening a PDF document, resolving its cross-reference table, catalog, pages and
/// document-level resources into an <see cref="IPdfDocument"/>.
/// </summary>
public class PdfDocumentReader
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly FontProvider _fontProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the reader with a logger factory and font provider for non-embedded font substitution.
    /// </summary>
    public PdfDocumentReader(ILoggerFactory loggerFactory, FontProvider fontProvider)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _fontProvider = fontProvider ?? throw new ArgumentNullException(nameof(fontProvider));
        _logger = loggerFactory.CreateLogger<PdfDocumentReader>();
    }

    /// <summary>
    /// Reads a PDF document from the specified stream, optionally using a password for decryption.
    /// </summary>
    /// <remarks>The returned document parses lazily from <paramref name="stream"/>; it is not copied.
    /// The stream must stay open, readable and seekable for as long as the document is in use.</remarks>
    /// <param name="stream">The input <see cref="Stream"/> containing the PDF data. The stream must be readable and seekable.</param>
    /// <param name="password">An optional password used to decrypt the PDF, if it is encrypted. If the PDF is not encrypted, this
    /// parameter can be <see langword="null"/>.</param>
    /// <returns>A <see cref="PdfDocument"/> representing the parsed PDF content. If the stream is empty, an empty <see
    /// cref="PdfDocument"/> is returned.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="stream"/> is not readable or does not support seeking.</exception>
    /// <exception cref="PdfInvalidDocumentException">Thrown if the PDF structure cannot be parsed.</exception>
    /// <exception cref="PdfIncorrectPasswordException">Thrown if the document is encrypted and the supplied password is incorrect.</exception>
    public IPdfDocument Read(Stream stream, string? password = null)
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
            return new PdfDocument(_loggerFactory, _fontProvider, stream);
        }

        IPdfDocumentInternal document = new PdfDocument(_loggerFactory, _fontProvider, stream);
        document.Password = password;
        document.HeaderOffset = PdfByteScanner.LocateHeader(document.Stream);

        if (document.HeaderOffset != 0)
        {
            _logger.LogWarning("PDF header found at offset {Offset}; declared offsets are relative to it.", document.HeaderOffset);
        }

        PdfXrefLoader xrefLoader = new(document);

        try
        {
            xrefLoader.LoadXref();
        }
        catch (PdfInvalidDocumentException ex)
        {
            _logger.LogWarning(ex, "Xref loading failed; attempting recovery scan.");
        }

        if (document.RootObject == null)
        {
            _logger.LogInformation("Xref incomplete (no catalog root); starting recovery scan.");
            document.ObjectCache.RunRecoveryScan();
        }

        if (document.RootObject == null)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document: catalog root not found.");
        }

        PdfPageExtractor pageExtractor = new(document);
        PdfOutputIntentParser outputIntentParser = new(document.RootObject, _loggerFactory.CreateLogger<PdfOutputIntentParser>());

        try
        {
            pageExtractor.ExtractPages();

            PdfOptionalContentGroupParser ocgParser = new(document.RootObject, _loggerFactory.CreateLogger<PdfOptionalContentGroupParser>());
            ((PdfDocument)document).OptionalContentGroups = ocgParser.Parse();

            ((PdfDocument)document).StructureTree = PdfStructureTree.FromCatalog(document.RootObject.Dictionary);

            IccProfile? outputIntentProfile = outputIntentParser.ParseFirstOutputIntentProfile();
            document.ObjectCache.OutputIntentProfile = outputIntentProfile;

            if (outputIntentProfile != null && outputIntentProfile.ChannelsCount != 0)
            {
                document.ObjectCache.OutputIntentConverter = new PdfIccColorSpaceConverter(outputIntentProfile.ChannelsCount, default, outputIntentProfile);
            }

            _logger.LogInformation("Parsed PDF with {PageCount} page(s).", document.Pages.Count);
        }
        catch (PdfIncorrectPasswordException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document.", ex);
        }

        return document;
    }
}
