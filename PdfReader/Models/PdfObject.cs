using PdfReader.Streams;
using System;
using System.IO;

namespace PdfReader.Models;

/// <summary>
/// Represents a parsed PDF object, including its reference, value, dictionary, and stream data.
/// </summary>
public class PdfObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfObject"/> class.
    /// </summary>
    /// <param name="reference">The PDF reference for this object.</param>
    /// <param name="document">The owning PDF document.</param>
    /// <param name="value">The value of the PDF object.</param>
    public PdfObject(PdfReference reference, PdfDocument document, IPdfValue value)
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
    public PdfDocument Document { get; }

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
    public bool HasStream => StreamInfo.HasValue;

    /// <summary>
    /// Embedded stream data, if available.
    /// </summary>
    internal ReadOnlyMemory<byte> EmbaddedStream { get; set; }

    /// <summary>
    /// Gets the raw stream for this object, handling embedded data, encryption, and subrange extraction.
    /// </summary>
    /// <returns>A <see cref="Stream"/> containing the raw stream data, or <see cref="Stream.Null"/> if not available.</returns>
    public Stream GetRawStream()
    {
        if (!EmbaddedStream.IsEmpty)
        {
            return new MemoryStream(EmbaddedStream.ToArray());
        }

        if (!HasStream)
        {
            return Stream.Null;
        }

        var subrange = new SubrangeReadOnlyStream(Document.Stream, StreamInfo.Value.Offset, StreamInfo.Value.Length, leaveOpen: true);

        if (StreamInfo.Value.IsEncrypted && Document.Decryptor != null && Reference.IsValid)
        {
            return Document.Decryptor.DecryptStream(subrange, Reference);
        }
        else
        {
            return subrange;
        }
    }

    /// <summary>
    /// Decodes the object's stream using the document's stream decoder and returns a readable <see cref="Stream"/>.
    /// </summary>
    /// <returns>A <see cref="Stream"/> containing the decoded stream data.</returns>
    public Stream DecodeAsStream()
    {
        return Document.StreamDecoder.DecodeContentAsStream(this);
    }

    /// <summary>
    /// Decodes the object's stream using the document's stream decoder and returns the decoded bytes as memory.
    /// </summary>
    /// <returns>A <see cref="ReadOnlyMemory{byte}"/> containing the decoded stream data.</returns>
    public ReadOnlyMemory<byte> DecodeAsMemory()
    {
        return Document.StreamDecoder.DecodeContentStream(this);
    }
}