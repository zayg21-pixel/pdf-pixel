using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Cache;
using PdfPixel.Encryption;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using PdfPixel.TextExtraction;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Models;

/// <summary>
/// Represents a parsed PDF document, exposing its pages and providing resource management for PDF processing.
/// </summary>
internal class PdfDocument : IPdfDocumentInternal
{
    private readonly ILogger<PdfDocument> _logger;
    private readonly List<IPdfPageInternal> _pages = [];
#pragma warning disable CA2213 // Disposable fields should be disposed
    private readonly FontProvider _fontProvider;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private readonly PdfDocumentObjectCache _objectCache;
    private readonly CMapCache _cMapCache;
    private readonly PdfStreamDecoder _streamDecoder;
    private readonly BufferedStream _stream;
    private readonly PdfDestinationResolver _destinationResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDocument"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="fontProvider">The font provider for font substitution and resolution.</param>
    /// <param name="fileStream">The input stream containing the PDF file data.</param>
    public PdfDocument(ILoggerFactory loggerFactory, FontProvider fontProvider, Stream fileStream)
    {
        LoggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PdfDocument>();
        _streamDecoder = new PdfStreamDecoder(loggerFactory);
        _fontProvider = fontProvider;
        _objectCache = new PdfDocumentObjectCache(this, new PdfObjectParser(this));
        _stream = new BufferedStream(fileStream);
        _cMapCache = new CMapCache(_logger);
        _destinationResolver = new PdfDestinationResolver(this);
    }

    /// <inheritdoc/>
    public ILoggerFactory LoggerFactory { get; }

    /// <inheritdoc />
    public CommandCache CommandCache { get; } = new();

    IReadOnlyList<IPdfPage> IPdfDocument.Pages => _pages;

    /// <inheritdoc />
    public IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> OptionalContentGroups { get; internal set; } = new Dictionary<PdfReference, PdfOptionalContentGroup>();

    /// <inheritdoc />
    public PdfStructureTree? StructureTree { get; internal set; }

    List<IPdfPageInternal> IPdfDocumentInternal.Pages => _pages;

    PdfDestinationResolver IPdfDocumentInternal.Destinations => _destinationResolver;

    PdfObject? IPdfDocumentInternal.RootObject { get; set; }

    int IPdfDocumentInternal.HeaderOffset { get; set; }

    BasePdfDecryptor? IPdfDocumentInternal.Decryptor { get; set; }

    string? IPdfDocumentInternal.Password { get; set; }

    FontProvider IPdfDocumentInternal.FontProvider => _fontProvider;

    PdfDocumentObjectCache IPdfDocumentInternal.ObjectCache => _objectCache;

    CMapCache IPdfDocumentInternal.CMapCache => _cMapCache;

    PdfStreamDecoder IPdfDocumentInternal.StreamDecoder => _streamDecoder;

    BufferedStream IPdfDocumentInternal.Stream => _stream;

    /// <inheritdoc/>
    public void Dispose()
    {
        CommandCache.Dispose();

        foreach (IPdfTypeface typeface in _objectCache.Typefaces.Values)
        {
            typeface.Dispose();
        }

        _fontProvider.Cleanup();
        _stream.Dispose();
    }
}
