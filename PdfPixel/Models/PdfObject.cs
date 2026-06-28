using PdfPixel.Commands;
using PdfPixel.Streams;
using System;
using System.IO;

namespace PdfPixel.Models;

/// <summary>
/// Represents a parsed PDF object, including its reference, value, dictionary, and stream data.
/// </summary>
public class PdfObject
{
    private PdfObjectStream? _stream;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfObject"/> class.
    /// </summary>
    /// <param name="reference">The PDF reference for this object.</param>
    /// <param name="document">The owning PDF document.</param>
    /// <param name="value">The value of the PDF object.</param>
    internal PdfObject(in PdfReference reference, IPdfDocumentInternal document, IPdfValue value)
    {
        Reference = reference;
        Document = document;
        Value = value;
        Dictionary = value.AsDictionary() ?? new PdfDictionary(document);
    }

    /// <summary>
    /// Gets the PDF reference for this object.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// Gets the owning PDF document.
    /// </summary>
    internal IPdfDocumentInternal Document { get; }

    /// <summary>
    /// Gets the value of the PDF object.
    /// </summary>
    public IPdfValue Value { get; }

    /// <summary>
    /// Gets the dictionary associated with this PDF object.
    /// </summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>
    /// Gets or sets the stream reference information for this object, if it has an associated stream.
    /// </summary>
    public PdfObjectStreamReference? StreamInfo { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether this object has an associated stream.
    /// </summary>
    public bool HasStream => StreamInfo.HasValue || !EmbaddedStream.IsEmpty;

    /// <summary>
    /// Embedded stream data, if available.
    /// </summary>
    internal ReadOnlyMemory<byte> EmbaddedStream { get; set; }

    /// <summary>
    /// Gets the self-contained stream source for this object, created lazily on first access.
    /// </summary>
    public PdfObjectStream Stream => _stream ??= PdfObjectStream.FromPdfObject(this);

    /// <summary>
    /// Decodes the object's stream using the document's stream decoder and returns a readable <see cref="System.IO.Stream"/>.
    /// </summary>
    public System.IO.Stream DecodeAsStream() => Stream.DecodeAsStream();

    /// <summary>
    /// Decodes the object's stream using the document's stream decoder and returns the decoded bytes as memory.
    /// </summary>
    public ReadOnlyMemory<byte> DecodeAsMemory(IPdfExecutionObserver? observer = default) => Stream.DecodeAsMemory(observer);
}
