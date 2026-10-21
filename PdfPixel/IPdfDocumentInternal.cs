using PdfPixel.Color.ColorSpace;
using PdfPixel.Encryption.Decryption;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel;

/// <summary>
/// Internal view of a PDF document, exposing infrastructure members used by parsers and renderers.
/// </summary>
internal interface IPdfDocumentInternal : IPdfDocument
{
    /// <summary>
    /// Gets the pages of the PDF document.
    /// </summary>
    new IReadOnlyList<IPdfPageInternal> Pages { get; }

    /// <summary>
    /// Reader of the document's name trees and number trees.
    /// </summary>
    PdfTreeReader TreeReader { get; }

    /// <summary>
    /// Gets the object cache for PDF objects in the document.
    /// </summary>
    PdfDocumentObjectCache ObjectCache { get; }

    /// <summary>
    /// Converter of the catalog output intent profile used for device colors. Null when no catalog output
    /// intent has a usable profile.
    /// </summary>
    PdfIccColorSpaceConverter? OutputIntentConverter { get; }

    /// <summary>
    /// Gets the resolver of the document's color spaces.
    /// </summary>
    PdfColorSpaceResolver ColorSpaceResolver { get; }

    /// <summary>
    /// Gets the content host of the document itself, with no resources.
    /// </summary>
    IPdfContentHost ContentHost { get; }

    /// <summary>
    /// Gets the CMap cache manager for the document.
    /// </summary>
    CMapCache CMapCache { get; }

    /// <summary>
    /// Gets the stream decoder for decoding PDF streams.
    /// </summary>
    PdfStreamDecoder StreamDecoder { get; }

    /// <summary>
    /// Gets the decryptor for encrypted PDF content.
    /// </summary>
    PdfDecryptor? Decryptor { get; }

    /// <summary>
    /// Gets the original PDF file stream for internal parser use (lazy object loading).
    /// </summary>
    BufferedStream Stream { get; }
}
