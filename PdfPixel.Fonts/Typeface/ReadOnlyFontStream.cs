using PdfPixel.Fonts.Sfnt;
using System;
using System.IO;

namespace PdfPixel.Fonts.Typeface;

/// <summary>
/// A read-only passthrough view over a seekable stream, with its own independent position so
/// multiple views over the same underlying stream can be read concurrently without interfering
/// with each other's cursor.
/// </summary>
public sealed class ReadOnlyFontStream : Stream
{
    private readonly Stream _innerStream;
    private readonly bool _leaveOpen;
    private long _position;
    private bool _disposed;

    private ReadOnlyFontStream(Stream innerStream, bool leaveOpen)
    {
        _innerStream = innerStream;
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Wraps <paramref name="source"/> in a read-only, independently-positioned view. If
    /// <paramref name="source"/> is not seekable, it is read to completion into a <see cref="MemoryStream"/>
    /// immediately, and disposed right away if <paramref name="leaveOpen"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="source">The stream to wrap.</param>
    /// <param name="leaveOpen">If <see langword="true"/>, disposing the returned stream does not dispose <paramref name="source"/>.</param>
    public static ReadOnlyFontStream Create(Stream source, bool leaveOpen)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.CanSeek)
        {
            return new ReadOnlyFontStream(source, leaveOpen);
        }

        MemoryStream bufferedStream = new();
        source.CopyTo(bufferedStream);
        bufferedStream.Position = 0;

        if (!leaveOpen)
        {
            source.Dispose();
        }

        return new ReadOnlyFontStream(bufferedStream, leaveOpen: false);
    }

    /// <summary>
    /// Gets a fresh, empty <see cref="ReadOnlyFontStream"/>, for callers with no source stream to
    /// resolve ranges against (e.g. a font assembled entirely from synthesized tables).
    /// </summary>
    public static ReadOnlyFontStream Empty => new(new MemoryStream(Array.Empty<byte>()), leaveOpen: false);

    /// <inheritdoc/>
    public override bool CanRead => !_disposed;

    /// <inheritdoc/>
    public override bool CanSeek => !_disposed;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _innerStream.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
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
            throw new ObjectDisposedException(nameof(ReadOnlyFontStream));
        }

        long remaining = Length - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        var toRead = (int)Math.Min(count, remaining);

        if (_innerStream.Position != _position)
        {
            _innerStream.Position = _position;
        }

        int read = _innerStream.Read(buffer, offset, toRead);
        _position += read;
        return read;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReadOnlyFontStream));
        }

        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0 || newPosition > Length)
        {
            throw new IOException("Cannot seek outside the stream bounds.");
        }

        _position = newPosition;
        return _position;
    }

    /// <summary>
    /// Reads exactly <paramref name="length"/> bytes starting at <paramref name="start"/> into a fresh
    /// array, independent of this stream's current <see cref="Position"/>. The returned array's length
    /// is trimmed to the number of bytes actually read, if the stream ended early.
    /// </summary>
    /// <param name="start">The absolute byte offset to start reading from.</param>
    /// <param name="length">The number of bytes to read.</param>
    public byte[] GetBytes(int start, int length)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReadOnlyFontStream));
        }

        var buffer = new byte[length];

        if (_innerStream.Position != start)
        {
            _innerStream.Position = start;
        }

        int totalRead = 0;
        while (totalRead < length)
        {
            int read = _innerStream.Read(buffer, totalRead, length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        _position = start + totalRead;

        if (totalRead < length)
        {
            Array.Resize(ref buffer, totalRead);
        }

        return buffer;
    }

    /// <summary>
    /// Reads exactly <paramref name="length"/> bytes starting at <paramref name="start"/>, independent
    /// of this stream's current <see cref="Position"/>.
    /// </summary>
    /// <param name="start">The absolute byte offset to start reading from.</param>
    /// <param name="length">The number of bytes to read.</param>
    public ReadOnlyMemory<byte> GetMemory(int start, int length) => GetBytes(start, length);

    /// <summary>
    /// Reads the bytes described by <paramref name="record"/>'s range.
    /// </summary>
    /// <param name="record">The table record describing the range to read.</param>
    public ReadOnlyMemory<byte> GetMemory(in SfntTableRecord record) => GetMemory(record.Offset, record.Length);

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException("ReadOnlyFontStream is read-only.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("ReadOnlyFontStream is read-only.");

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
