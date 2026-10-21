using PdfPixel.Streams;
using System;
using System.IO;

namespace PdfPixel.Objects;

/// <summary>
/// Represents a parsed PDF object, including its reference, value, dictionary, and stream data.
/// </summary>
public class PdfObject
{
    private PdfObjectStream? _stream;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfObject"/> class.
    /// </summary>
    /// <param name="reference">The PDF reference for this object, or <see langword="null"/> for a direct object.</param>
    /// <param name="document">The owning PDF document.</param>
    /// <param name="value">The value of the PDF object.</param>
    internal PdfObject(PdfReference? reference, IPdfDocumentInternal document, PdfValue value)
    {
        Reference = reference;
        Document = document;
        Value = value;
        Dictionary = value.AsDictionary() ?? new PdfDictionary(document);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfObject"/> class with a stream stored in the file.
    /// </summary>
    /// <param name="reference">The PDF reference for this object, or <see langword="null"/> for a direct object.</param>
    /// <param name="document">The owning PDF document.</param>
    /// <param name="value">The value of the PDF object.</param>
    /// <param name="streamInfo">The location of the object's stream data, or <see langword="null"/> when it has none.</param>
    internal PdfObject(PdfReference? reference, IPdfDocumentInternal document, PdfValue value, PdfObjectStreamReference? streamInfo)
        : this(reference, document, value)
    {
        StreamInfo = streamInfo;
    }

    /// <summary>
    /// Initializes a new direct instance of the <see cref="PdfObject"/> class with inline data.
    /// </summary>
    /// <param name="document">The owning PDF document.</param>
    /// <param name="value">The value of the PDF object.</param>
    /// <param name="inlineData">The inline data.</param>
    internal PdfObject(IPdfDocumentInternal document, PdfValue value, byte[] inlineData)
        : this(null, document, value)
    {
        InlineData = inlineData;
    }

    /// <summary>
    /// Gets the PDF reference for this object, or <see langword="null"/> when it is a direct object.
    /// </summary>
    public PdfReference? Reference { get; }

    /// <summary>
    /// Gets the owning PDF document.
    /// </summary>
    internal IPdfDocumentInternal Document { get; }

    /// <summary>
    /// Gets the value of the PDF object.
    /// </summary>
    public PdfValue Value { get; }

    /// <summary>
    /// Gets the dictionary associated with this PDF object.
    /// </summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>
    /// Gets the stream reference information for this object, if it has an associated stream.
    /// </summary>
    public PdfObjectStreamReference? StreamInfo { get; }

    /// <summary>
    /// Gets a value indicating whether this object has an associated stream.
    /// </summary>
    public bool HasStream => StreamInfo.HasValue || InlineData != null;

    /// <summary>
    /// Inline data, if available.
    /// </summary>
    internal byte[]? InlineData { get; }

    /// <summary>
    /// Gets the self-contained stream source for this object, created lazily on first access.
    /// </summary>
    public PdfObjectStream Stream => _stream ??= PdfObjectStream.FromPdfObject(this);
}
