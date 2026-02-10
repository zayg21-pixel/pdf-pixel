using Microsoft.Extensions.Logging;
using PdfPixel.Encryption;
using PdfPixel.Fonts.Management;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using PdfPixel.Fonts.Mapping;

namespace PdfPixel.Models;
// TODO: [MEDIUM] minimize references to PDF Document, use interface instead for unit testing later

/// <summary>
/// Represents a parsed PDF document, exposing its pages and providing resource management for PDF processing.
/// </summary>
public class PdfDocument : IDisposable
{
    private readonly ILogger<PdfDocument> _logger;

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
        StreamDecoder = new PdfStreamDecoder(loggerFactory);
        FontSubstitutor = new SkiaFontSubstitutor(fontProvider);
        ObjectCache = new PdfDocumentObjectCache(new PdfObjectParser(this));
        Stream = new BufferedStream(fileStream);
        CMapCache = new CMapCache(this, _logger);
    }

    /// <summary>
    /// Gets the list of pages in the PDF document.
    /// </summary>
    public List<PdfPage> Pages { get; } = new List<PdfPage>();

    /// <summary>
    /// Gets or sets the root object of the PDF document.
    /// </summary>
    internal PdfObject RootObject { get; set; }

    /// <summary>
    /// Gets the logger factory used for creating loggers.
    /// </summary>
    internal ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets the document-level font substitution engine.
    /// </summary>
    internal SkiaFontSubstitutor FontSubstitutor { get; }

    /// <summary>
    /// Gets the object cache for PDF objects in the document.
    /// </summary>
    internal PdfDocumentObjectCache ObjectCache { get; }

    /// <summary>
    /// Gets the CMap cache manager for the document.
    /// </summary>
    internal CMapCache CMapCache { get; }

    /// <summary>
    /// Gets the stream decoder for decoding PDF streams.
    /// </summary>
    internal PdfStreamDecoder StreamDecoder { get; }

    /// <summary>
    /// Gets or sets the decryptor for encrypted PDF content.
    /// </summary>
    internal BasePdfDecryptor Decryptor { get; set; }

    /// <summary>
    /// Gets the original PDF file stream for internal parser use (lazy object loading).
    /// </summary>
    internal BufferedStream Stream { get; }

    /// <summary>
    /// Releases all resources used by the <see cref="PdfDocument"/>.
    /// </summary>
    public void Dispose()
    {
        Stream.Dispose();
    }
}