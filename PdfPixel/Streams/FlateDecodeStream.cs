using System;
using System.IO;
using System.IO.Compression;

namespace PdfPixel.Streams;

/// <summary>
/// Forward-only stream that decodes PDF FlateDecode (ISO 32000-1, 7.4.4).
/// </summary>
internal sealed class FlateDecodeStream : Stream
{
    private readonly DeflateStream _deflateStream;
    private bool _endOfStream;

    /// <summary>
    /// Initializes the decoder wrapping a stream positioned after the 2-byte zlib header.
    /// </summary>
    public FlateDecodeStream(Stream baseStream)
    {
        if (baseStream == null)
        {
            throw new ArgumentNullException(nameof(baseStream));
        }

        _deflateStream = new DeflateStream(baseStream, CompressionMode.Decompress, leaveOpen: false);
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_endOfStream)
        {
            return 0;
        }

        try
        {
            int bytesRead = _deflateStream.Read(buffer, offset, count);
            if (bytesRead == 0)
            {
                _endOfStream = true;
            }

            return bytesRead;
        }
        catch (InvalidDataException)
        {
            _endOfStream = true;
            return 0;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _deflateStream.Dispose();
        }

        base.Dispose(disposing);
    }
}
