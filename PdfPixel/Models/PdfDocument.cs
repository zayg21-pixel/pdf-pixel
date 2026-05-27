using Microsoft.Extensions.Logging;
using PdfPixel.Encryption;
using PdfPixel.Fonts.Management;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using System.Collections.Generic;
using System.IO;
using PdfPixel.Fonts.Mapping;

namespace PdfPixel.Models;

/// <summary>
/// Represents a parsed PDF document, exposing its pages and providing resource management for PDF processing.
/// </summary>
internal class PdfDocument : IPdfDocumentInternal
{
    private readonly ILogger<PdfDocument> _logger;
    private readonly List<IPdfPageInternal> _pages = [];
    private readonly SkiaFontSubstitutor _fontSubstitutor;
    private readonly PdfDocumentObjectCache _objectCache;
    private readonly CMapCache _cMapCache;
    private readonly PdfStreamDecoder _streamDecoder;
    private readonly BufferedStream _stream;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDocument"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="fontProvider">The font provider for font substitution and resolution.</param>
    /// <param name="fileStream">The input stream containing the PDF file data.</param>
    public PdfDocument(ILoggerFactory loggerFactory, ISkiaFontProvider fontProvider, Stream fileStream)
    {
        LoggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PdfDocument>();
        _streamDecoder = new PdfStreamDecoder(loggerFactory);
        _fontSubstitutor = new SkiaFontSubstitutor(fontProvider);
        _objectCache = new PdfDocumentObjectCache(new PdfObjectParser(this));
        _stream = new BufferedStream(fileStream);
        _cMapCache = new CMapCache(this, _logger);
    }

    /// <inheritdoc/>
    public ILoggerFactory LoggerFactory { get; }

    IReadOnlyList<IPdfPage> IPdfDocument.Pages => _pages;

    List<IPdfPageInternal> IPdfDocumentInternal.Pages => _pages;

    PdfDictionary? IPdfDocumentInternal.NamedDestinations { get; set; }

    PdfObject? IPdfDocumentInternal.RootObject { get; set; }

    BasePdfDecryptor? IPdfDocumentInternal.Decryptor { get; set; }

    SkiaFontSubstitutor IPdfDocumentInternal.FontSubstitutor => _fontSubstitutor;

    PdfDocumentObjectCache IPdfDocumentInternal.ObjectCache => _objectCache;

    CMapCache IPdfDocumentInternal.CMapCache => _cMapCache;

    PdfStreamDecoder IPdfDocumentInternal.StreamDecoder => _streamDecoder;

    BufferedStream IPdfDocumentInternal.Stream => _stream;

    /// <inheritdoc/>
    public void Dispose() => _stream.Dispose();
}
