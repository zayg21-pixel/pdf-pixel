using Microsoft.Extensions.Logging;
using PdfPixel.Encryption;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Streams;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Models;

/// <summary>
/// Internal view of a PDF document, exposing infrastructure members used by parsers and renderers.
/// </summary>
internal interface IPdfDocumentInternal : IPdfDocument
{
    /// <summary>
    /// Gets the logger factory used for creating loggers.
    /// </summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets the mutable list of pages in the PDF document.
    /// </summary>
    new List<IPdfPageInternal> Pages { get; }

    /// <summary>
    /// Gets or sets the named destinations dictionary from the PDF document catalog.
    /// Can originate from a <c>/Dests</c> entry (older format) or a <c>/Names/Dests</c> entry (newer format).
    /// </summary>
    PdfDictionary? NamedDestinations { get; set; }

    /// <summary>
    /// Gets or sets the root object of the PDF document.
    /// </summary>
    PdfObject? RootObject { get; set; }

    /// <summary>
    /// Gets the document-level font substitution engine.
    /// </summary>
    SkiaFontSubstitutor FontSubstitutor { get; }

    /// <summary>
    /// Gets the object cache for PDF objects in the document.
    /// </summary>
    PdfDocumentObjectCache ObjectCache { get; }

    /// <summary>
    /// Gets the CMap cache manager for the document.
    /// </summary>
    CMapCache CMapCache { get; }

    /// <summary>
    /// Gets the stream decoder for decoding PDF streams.
    /// </summary>
    PdfStreamDecoder StreamDecoder { get; }

    /// <summary>
    /// Gets or sets the decryptor for encrypted PDF content.
    /// </summary>
    BasePdfDecryptor? Decryptor { get; set; }

    /// <summary>
    /// Gets the original PDF file stream for internal parser use (lazy object loading).
    /// </summary>
    BufferedStream Stream { get; }
}
