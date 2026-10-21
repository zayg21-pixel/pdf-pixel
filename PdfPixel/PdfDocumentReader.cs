using Microsoft.Extensions.Logging;
using PdfPixel.Encryption;
using PdfPixel.Fonts.Substitution;
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
    private readonly IFontSubstitutor _fontSubstitutor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the reader with a logger factory and font source for non-embedded font substitution.
    /// </summary>
    public PdfDocumentReader(ILoggerFactory loggerFactory, IFontSubstitutor fontSubstitutor)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _fontSubstitutor = fontSubstitutor ?? throw new ArgumentNullException(nameof(fontSubstitutor));
        _logger = loggerFactory.CreateLogger<PdfDocumentReader>();
    }

    /// <summary>
    /// Reads a PDF document from the specified stream, requesting a credential when an encrypted document needs one.
    /// </summary>
    /// <remarks>The returned document parses lazily from <paramref name="stream"/>; it is not copied.
    /// The stream must stay open, readable and seekable for as long as the document is in use.</remarks>
    /// <param name="stream">The input <see cref="Stream"/> containing the PDF data. The stream must be readable and seekable.</param>
    /// <param name="onCredentialRequested">Called when the empty user password does not decrypt the document, at open or on
    /// first access to encrypted embedded files. When <see langword="null"/>, only the empty user password is tried.</param>
    /// <returns>A <see cref="PdfDocument"/> representing the parsed PDF content.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="stream"/> is not readable or does not support seeking.</exception>
    /// <exception cref="PdfInvalidDocumentException">Thrown if the PDF structure cannot be parsed.</exception>
    /// <exception cref="PdfAuthenticationException">Thrown if the document requires a credential at open and none of the supplied credentials is accepted.</exception>
    /// <exception cref="NotSupportedException">Thrown if the document uses a feature that is not supported.</exception>
    public IPdfDocument Read(Stream stream, PdfCredentialRequestedCallback? onCredentialRequested = null)
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

        PdfDocument document;

        try
        {
            document = new PdfDocument(_loggerFactory, _fontSubstitutor, stream, onCredentialRequested);
        }
        catch (PdfAuthenticationException)
        {
            throw;
        }
        catch (PdfInvalidDocumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document.", ex);
        }

        IPdfDocument publicDocument = document;
        if (publicDocument.Pages.Count == 0)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document: no pages found.");
        }

        _logger.LogInformation("Parsed PDF with {PageCount} page(s).", publicDocument.Pages.Count);

        return document;
    }

    /// <summary>
    /// Reads a PDF document from the specified stream, using a password for decryption.
    /// </summary>
    /// <param name="stream">The input <see cref="Stream"/> containing the PDF data. The stream must be readable and seekable.</param>
    /// <param name="password">The password used to decrypt the PDF, if it is encrypted.</param>
    /// <returns>A <see cref="PdfDocument"/> representing the parsed PDF content.</returns>
    /// <exception cref="PdfAuthenticationException">Thrown if the document is encrypted and the supplied password is incorrect.</exception>
    [Obsolete("Use Read(Stream, PdfCredentialRequestedCallback?) and supply the password from the callback instead.")]
    public IPdfDocument Read(Stream stream, string? password)
        => Read(stream, request => (request.Reason == PdfCredentialRequestReason.CredentialRequired && password != null) ? new PdfPasswordCredential(password) : null);
}
