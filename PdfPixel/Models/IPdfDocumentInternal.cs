using PdfPixel.Encryption.Decryption;
using PdfPixel.Encryption.Model;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Parsing;
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
    /// Gets the mutable list of pages in the PDF document.
    /// </summary>
    new List<IPdfPageInternal> Pages { get; }

    /// <summary>
    /// Gets the resolver for the destinations of the document, explicit and named alike.
    /// </summary>
    PdfDestinationResolver Destinations { get; }

    /// <summary>
    /// Reader of the document's name trees and number trees.
    /// </summary>
    PdfTreeReader TreeReader { get; }

    /// <summary>
    /// Gets or sets the root object of the PDF document.
    /// </summary>
    PdfObject? RootObject { get; set; }

    /// <summary>
    /// Gets or sets the absolute file position of the <c>%PDF-</c> header. Offsets the document declares
    /// for itself (startxref, cross-reference entries) are relative to this position, which is non-zero
    /// only when the file carries junk bytes ahead of the header.
    /// </summary>
    int HeaderOffset { get; set; }

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
    /// Gets or sets the callback supplied by the caller for requesting the credential of this document.
    /// </summary>
    PdfCredentialRequestedCallback? OnCredentialRequested { get; set; }

    /// <summary>
    /// Gets the original PDF file stream for internal parser use (lazy object loading).
    /// </summary>
    BufferedStream Stream { get; }
}
