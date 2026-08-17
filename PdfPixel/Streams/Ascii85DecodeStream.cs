using System;
using System.IO;

namespace PdfPixel.Streams;

/// <summary>
/// Forward-only stream that decodes PDF ASCII85Decode (ISO 32000-1, 7.4.3.5).
/// </summary>
public sealed class Ascii85DecodeStream : Stream
{
    private readonly Stream _inner;
    private readonly bool _leaveOpen;

    private bool _endReached;
    private readonly int[] _groupDigits = new int[5];
    private int _groupLength;

    private readonly byte[] _buffer = new byte[4];
    private int _bufferOffset;
    private int _bufferCount;

    /// <summary>
    /// Initializes the decoder wrapping the given ASCII85-encoded stream.
    /// </summary>
    public Ascii85DecodeStream(Stream inner, bool leaveOpen = false)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (!inner.CanRead)
        {
            throw new ArgumentException("Inner stream must be readable", nameof(inner));
        }

        _leaveOpen = leaveOpen;
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
        // No-op.
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
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

        int written = 0;
        while (written < count)
        {
            if (_bufferCount == 0)
            {
                if (!FillDecodeBuffer())
                {
                    break;
                }
            }

            int toCopy = Math.Min(count - written, _bufferCount);
            Array.Copy(_buffer, _bufferOffset, buffer, offset + written, toCopy);
            _bufferOffset += toCopy;
            _bufferCount -= toCopy;
            written += toCopy;
        }

        return written;
    }

    private bool FillDecodeBuffer()
    {
        if (_endReached)
        {
            return false;
        }

        _bufferOffset = 0;
        _bufferCount = 0;

        while (true)
        {
            int b = _inner.ReadByte();
            if (b < 0)
            {
                _endReached = true;
                EmitFinalPartialGroupIfNeeded();
                return _bufferCount > 0;
            }

            if (IsWhiteSpace((byte)b))
            {
                continue;
            }

            if (b == 'z')
            {
                // 'z' expands to four zero bytes, and only at the start of a group.
                if (_groupLength == 0)
                {
                    _buffer[0] = 0;
                    _buffer[1] = 0;
                    _buffer[2] = 0;
                    _buffer[3] = 0;
                    _bufferCount = 4;
                    return true;
                }
                else
                {
                    continue;
                }
            }

            if (b == '~')
            {
                // Expect '>' end marker; consume if present.
                int next = _inner.ReadByte();
                if (next != '>')
                {
                    // Robustness: if '>' missing, treat '~' as end anyway.
                }

                _endReached = true;
                EmitFinalPartialGroupIfNeeded();
                return _bufferCount > 0;
            }

            if (b < '!' || b > 'u')
            {
                continue;
            }

            int digit = b - '!';
            _groupDigits[_groupLength] = digit;
            _groupLength++;

            if (_groupLength == 5)
            {
                uint value = 0;
                for (int i = 0; i < 5; i++)
                {
                    value = (value * 85) + (uint)_groupDigits[i];
                }

                _buffer[0] = (byte)(value >> 24);
                _buffer[1] = (byte)(value >> 16);
                _buffer[2] = (byte)(value >> 8);
                _buffer[3] = (byte)value;
                _bufferCount = 4;
                _groupLength = 0;
                return true;
            }
        }
    }

    private void EmitFinalPartialGroupIfNeeded()
    {
        if (_groupLength <= 1)
        {
            _groupLength = 0;
            return;
        }

        // Pad the group up to five digits with 'u' (84).
        for (int i = _groupLength; i < 5; i++)
        {
            _groupDigits[i] = 84;
        }

        uint value = 0;
        for (int i = 0; i < 5; i++)
        {
            value = (value * 85) + (uint)_groupDigits[i];
        }

        int bytesToEmit = _groupLength - 1;
        _buffer[0] = (byte)(value >> 24);
        _buffer[1] = (byte)(value >> 16);
        _buffer[2] = (byte)(value >> 8);
        _buffer[3] = (byte)value;
        _bufferOffset = 0;
        _bufferCount = bytesToEmit;
        _groupLength = 0;
    }

    private static bool IsWhiteSpace(byte b)
    {
        // PDF whitespace: NUL (0), HT (9), LF (10), FF (12), CR (13), Space (32)
        return b == 0 || b == 9 || b == 10 || b == 12 || b == 13 || b == 32;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
