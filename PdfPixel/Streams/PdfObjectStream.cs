using PdfPixel.Commands;
using PdfPixel.Encryption;
using PdfPixel.Encryption.Decryption;
using PdfPixel.Objects;
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
    private readonly byte[]? _inlineData;
    private readonly PdfObjectStreamReference? _streamReference;
    private readonly BufferedStream? _documentStream;
    private readonly PdfDecryptor? _decryptor;
    private readonly PdfCryptFilter _cryptFilter = PdfCryptFilter.Identity;
    private readonly PdfReference _objectReference;

    private PdfObjectStream(
        PdfStreamDecoder streamDecoder,
        List<PdfStreamFilter> filters,
        byte[]? inlineData)
    {
        _streamDecoder = streamDecoder;
        Filters = filters;
        _inlineData = inlineData;
    }

    private PdfObjectStream(
        PdfStreamDecoder streamDecoder,
        List<PdfStreamFilter> filters,
        PdfObjectStreamReference streamReference,
        BufferedStream documentStream,
        PdfDecryptor? decryptor,
        PdfCryptFilter cryptFilter,
        in PdfReference objectReference)
    {
        _streamDecoder = streamDecoder;
        Filters = filters;
        _streamReference = streamReference;
        _documentStream = documentStream;
        _decryptor = decryptor;
        _cryptFilter = cryptFilter;
        _objectReference = objectReference;
    }

    /// <summary>
    /// Gets the filter chain for this stream, each filter with its decode parameters.
    /// </summary>
    public IReadOnlyList<PdfStreamFilter> Filters { get; }

    /// <summary>
    /// Creates a <see cref="PdfObjectStream"/> over the data of an inline image, encoded with <paramref name="filters"/>.
    /// </summary>
    internal static PdfObjectStream FromInlineData(IPdfDocumentInternal document, List<PdfStreamFilter> filters, byte[] inlineData)
        => new(document.StreamDecoder, filters, inlineData);

    /// <summary>
    /// Creates a <see cref="PdfObjectStream"/> from a <see cref="PdfObject"/>.
    /// </summary>
    internal static PdfObjectStream FromPdfObject(PdfObject pdfObject)
    {
        IPdfDocumentInternal document = pdfObject.Document;
        List<PdfStreamFilter> filters = PdfStreamDecoder.GetStreamFilters(pdfObject.Dictionary);

        PdfCryptFilter cryptFilter = PdfCryptFilter.Identity;
        if (document.Decryptor != null)
        {
            cryptFilter = document.Decryptor.GetStreamCryptFilter(pdfObject.Dictionary, filters);
        }

        if (pdfObject.InlineData != null)
        {
            return new PdfObjectStream(
                document.StreamDecoder,
                filters,
                pdfObject.InlineData);
        }

        if (pdfObject.StreamInfo.HasValue && pdfObject.Reference != null)
        {
            return new PdfObjectStream(
                document.StreamDecoder,
                filters,
                pdfObject.StreamInfo.Value,
                document.Stream,
                document.Decryptor,
                cryptFilter,
                pdfObject.Reference.Value);
        }

        return new PdfObjectStream(
            document.StreamDecoder,
            filters,
            inlineData: null);
    }

    /// <summary>
    /// Gets whether this stream has any content to read or decode.
    /// </summary>
    public bool HasContent
    {
        get
        {
            return (_inlineData != null && _inlineData.Length != 0)
                ||(_streamReference.HasValue && _streamReference.Value.Length != 0 && _documentStream != null);
        }
    }

    /// <summary>
    /// Gets the raw, undecoded stream.
    /// </summary>
    public Stream GetRawStream()
    {
        if (_inlineData != null)
        {
            return new MemoryStream(_inlineData, writable: false);
        }

        if (!_streamReference.HasValue || _streamReference.Value.Length == 0 || _documentStream == null)
        {
            return Stream.Null;
        }

        PdfObjectStreamReference streamReference = _streamReference.Value;
        SubrangeReadOnlyStream subrange = new(_documentStream, streamReference.Offset, streamReference.Length, leaveOpen: true);

        if (streamReference.IsEncrypted && _decryptor != null)
        {
            return _decryptor.DecryptStream(subrange, _objectReference, _cryptFilter);
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

        return new BufferedStream(_streamDecoder.DecodeContentAsStream(GetRawStream(), Filters), PdfStreamDecoder.StreamChunkSize);
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

        int rawLength = (_inlineData != null) ? _inlineData.Length : _streamReference.GetValueOrDefault().Length;

        return _streamDecoder.DecodeContentStream(GetRawStream(), rawLength, Filters, observer);
    }
}
