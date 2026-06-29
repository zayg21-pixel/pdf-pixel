using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Fonts.Management;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Text;
using PdfPixel.TextExtraction;
using System;
using System.IO;

namespace PdfPixel;

/// <summary>
/// Main entry point for reading PDF documents.
/// </summary>
public class PdfDocumentReader
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ISkiaFontProvider _skiaFontProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the reader with a logger factory and font provider for non-embedded font substitution.
    /// </summary>
    public PdfDocumentReader(ILoggerFactory loggerFactory, ISkiaFontProvider fontProvider)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _skiaFontProvider = fontProvider ?? throw new ArgumentNullException(nameof(fontProvider));
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
            return new PdfDocument(_loggerFactory, _skiaFontProvider, stream);
        }

        IPdfDocumentInternal document = new PdfDocument(_loggerFactory, _skiaFontProvider, stream);
        using PdfXrefLoader xrefLoader = new(document);

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
            PdfXrefRecoveryScanner recoveryScanner = new(document);
            recoveryScanner.Scan();
        }

        if (document.RootObject == null)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document: catalog root not found.");
        }

        PdfPageExtractor pageExtractor = new(document);
        PdfNamedDestinationParser namedDestinationParser = new(document);
        PdfOutputIntentParser outputIntentParser = new(document.RootObject, _loggerFactory.CreateLogger<PdfOutputIntentParser>());

        try
        {
            document.Decryptor?.UpdatePassword(password ?? string.Empty);

            document.NamedDestinations = namedDestinationParser.ParseNamedDestinations();
            pageExtractor.ExtractPages();

            PdfOptionalContentGroupParser ocgParser = new(document.RootObject, _loggerFactory.CreateLogger<PdfOptionalContentGroupParser>());
            ((PdfDocument)document).OptionalContentGroups = ocgParser.Parse();

            PdfDictionary? structTreeRoot = document.RootObject.Dictionary.GetDictionary(PdfTokens.StructTreeRootKey);
            ((PdfDocument)document).StructureTree = PdfStructureTree.FromDictionary(structTreeRoot, document);

            IccProfile? outputIntentProfile = outputIntentParser.ParseFirstOutputIntentProfile();
            document.ObjectCache.OutputIntentProfile = outputIntentProfile;

            if (outputIntentProfile != null && outputIntentProfile.ChannelsCount != 0)
            {
                document.ObjectCache.OutputIntentConverter = new IccBasedConverter(outputIntentProfile.ChannelsCount, default, outputIntentProfile);
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
