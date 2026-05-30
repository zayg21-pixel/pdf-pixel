using System;
using System.IO;

namespace PdfPixel.Streams;

/// <summary>
/// Provides a read-only window over a seekable underlying <see cref="Stream"/>.
/// The wrapper restricts all read and seek operations to a fixed subrange defined by
/// an absolute starting offset and a length. Attempts to access outside the range fail.
/// </summary>
public sealed class SubrangeReadOnlyStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _subrangeOffset;
    private readonly bool _leaveOpen;
    private long _position; // Position relative to the subrange start.
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubrangeReadOnlyStream"/> class.
    /// </summary>
    /// <param name="innerStream">Seekable source stream.</param>
    /// <param name="offset">Absolute start offset of the subrange within the source stream.</param>
    /// <param name="length">Length of the subrange in bytes.</param>
    /// <param name="leaveOpen">If true, does not dispose the inner stream when this wrapper is disposed.</param>
    public SubrangeReadOnlyStream(Stream innerStream, long offset, long length, bool leaveOpen = true)
    {
        if (innerStream == null)
        {
            throw new ArgumentNullException(nameof(innerStream));
        }

        if (!innerStream.CanSeek)
        {
            throw new ArgumentException("Inner stream must be seekable.", nameof(innerStream));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (innerStream.Length - offset < length)
        {
            throw new ArgumentException("Specified offset and length exceed inner stream bounds.");
        }

        _innerStream = innerStream;
        _subrangeOffset = offset;
        Length = length;
        _leaveOpen = leaveOpen;
        _position = 0;
    }

    /// <inheritdoc/>
    public override long Length { get; }

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc/>
    public override bool CanRead
    {
        get
        {
            return !_disposed && _innerStream.CanRead;
        }
    }

    /// <inheritdoc/>
    public override bool CanSeek
    {
        get
        {
            return !_disposed && _innerStream.CanSeek;
        }
    }

    /// <inheritdoc/>
    public override bool CanWrite
    {
        get
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // No-op: read-only.
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SubrangeReadOnlyStream));
        }

        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (offset + count > buffer.Length)
        {
            throw new ArgumentException("offset and count exceed buffer length.", nameof(count));
        }

        if (count == 0)
        {
            return 0;
        }

        if (_position >= Length)
        {
            return 0; // EOF of subrange.
        }

        long remaining = Length - _position;
        if (count > remaining)
        {
            count = (int)remaining;
        }

        // Align inner stream position with absolute offset.
        long absolute = _subrangeOffset + _position;
        if (_innerStream.Position != absolute)
        {
            _innerStream.Position = absolute;
        }

        int read = _innerStream.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SubrangeReadOnlyStream));
        }

        long newPos;
        switch (origin)
        {
            case SeekOrigin.Begin:
            {
                newPos = offset;
                break;
            }
            case SeekOrigin.Current:
            {
                newPos = _position + offset;
                break;
            }
            case SeekOrigin.End:
            {
                newPos = Length + offset;
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(origin));
            }
        }

        if (newPos < 0)
        {
            throw new IOException("Cannot seek before subrange start.");
        }

        if (newPos > Length)
        {
            throw new IOException("Cannot seek beyond subrange end.");
        }

        _position = newPos;
        return _position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException("SubrangeReadOnlyStream is fixed-length and read-only.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("SubrangeReadOnlyStream is read-only.");

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            if (!_leaveOpen)
            {
                _innerStream.Dispose();
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
