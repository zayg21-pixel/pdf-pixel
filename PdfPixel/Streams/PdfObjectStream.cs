using PdfPixel.Encryption;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Streams;

/// <summary>
/// Self-contained source for the raw and decoded data of a single PDF stream object.
/// </summary>
public sealed class PdfObjectStream
{
    private readonly PdfStreamDecoder _streamDecoder;
    private readonly ReadOnlyMemory<byte> _embeddedStream;
    private readonly PdfObjectStreamReference? _streamReference;
    private readonly BufferedStream? _documentStream;
    private readonly BasePdfDecryptor? _decryptor;
    private readonly PdfReference _objectReference;

    private PdfObjectStream(
        PdfStreamDecoder streamDecoder,
        List<PdfFilterType> filters,
        List<PdfDecodeParameters?> decodeParameters,
        in ReadOnlyMemory<byte> embeddedStream)
    {
        _streamDecoder = streamDecoder;
        Filters = filters;
        DecodeParameters = decodeParameters;
        _embeddedStream = embeddedStream;
    }

    private PdfObjectStream(
        PdfStreamDecoder streamDecoder,
        List<PdfFilterType> filters,
        List<PdfDecodeParameters?> decodeParameters,
        PdfObjectStreamReference streamReference,
        BufferedStream documentStream,
        BasePdfDecryptor? decryptor,
        in PdfReference objectReference)
    {
        _streamDecoder = streamDecoder;
        Filters = filters;
        DecodeParameters = decodeParameters;
        _streamReference = streamReference;
        _documentStream = documentStream;
        _decryptor = decryptor;
        _objectReference = objectReference;
    }

    /// <summary>
    /// Gets the filter chain for this stream.
    /// </summary>
    public List<PdfFilterType> Filters { get; }

    /// <summary>
    /// Gets the decode parameters for this stream.
    /// </summary>
    public List<PdfDecodeParameters?> DecodeParameters { get; }

    /// <summary>
    /// Creates a <see cref="PdfObjectStream"/> from a <see cref="PdfObject"/>.
    /// </summary>
    internal static PdfObjectStream FromPdfObject(PdfObject pdfObject)
    {
        IPdfDocumentInternal document = pdfObject.Document;
        List<PdfFilterType> filters = PdfStreamDecoder.GetFilters(pdfObject.Dictionary);
        List<PdfDecodeParameters?> decodeParameters = PdfStreamDecoder.GetDecodeParameters(pdfObject.Dictionary);

        if (!pdfObject.EmbaddedStream.IsEmpty)
        {
            return new PdfObjectStream(
                document.StreamDecoder,
                filters,
                decodeParameters,
                pdfObject.EmbaddedStream);
        }

        if (pdfObject.StreamInfo.HasValue)
        {
            return new PdfObjectStream(
                document.StreamDecoder,
                filters,
                decodeParameters,
                pdfObject.StreamInfo.Value,
                document.Stream,
                document.Decryptor,
                pdfObject.Reference);
        }

        return new PdfObjectStream(
            document.StreamDecoder,
            filters,
            decodeParameters,
            ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>
    /// Gets whether this stream has any content to read or decode.
    /// </summary>
    public bool HasContent
    {
        get
        {
            return !_embeddedStream.IsEmpty
                || (_streamReference.HasValue && _streamReference.Value.Length != 0 && _documentStream != null);
        }
    }

    /// <summary>
    /// Gets the raw, undecoded stream.
    /// </summary>
    public Stream GetRawStream()
    {
        if (!_embeddedStream.IsEmpty)
        {
            return new MemoryStream(_embeddedStream.ToArray());
        }

        if (!_streamReference.HasValue || _streamReference.Value.Length == 0 || _documentStream == null)
        {
            return Stream.Null;
        }

        PdfObjectStreamReference streamReference = _streamReference.Value;
        SubrangeReadOnlyStream subrange = new(_documentStream, streamReference.Offset, streamReference.Length, leaveOpen: true);

        if (streamReference.IsEncrypted && _decryptor != null && _objectReference.IsValid)
        {
            return _decryptor.DecryptStream(subrange, _objectReference);
        }

        return subrange;
    }

    /// <summary>
    /// Decodes the stream and returns a readable <see cref="Stream"/> (caller disposes).
    /// </summary>
    public Stream DecodeAsStream()
    {
        if (!HasContent)
        {
            return Stream.Null;
        }

        return _streamDecoder.DecodeContentAsStream(GetRawStream(), Filters, DecodeParameters);
    }

    /// <summary>
    /// Decodes the stream and returns the decoded bytes as memory.
    /// </summary>
    public ReadOnlyMemory<byte> DecodeAsMemory(IPdfExecutionObserver? observer = default)
    {
        if (!HasContent)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return _streamDecoder.DecodeContentStream(GetRawStream(), Filters, DecodeParameters, observer);
    }
}
