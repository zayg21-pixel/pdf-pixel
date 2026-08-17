using System;
using System.IO;

namespace PdfPixel.Streams;

/// <summary>
/// Forward-only stream that decodes PDF RunLengthDecode (ISO 32000-1, 7.4.5).
/// </summary>
public sealed class RunLengthDecodeStream : Stream
{
    private readonly Stream _baseStream;
    private readonly bool _leaveOpen;
    private bool _endOfStream;
    private int _repeatCount;
    private int _repeatByte;
    private int _bufferIndex;
    private byte[] _buffer;

    /// <summary>
    /// Initializes the decoder wrapping the given run-length encoded stream.
    /// </summary>
    public RunLengthDecodeStream(Stream baseStream, bool leaveOpen)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _leaveOpen = leaveOpen;
        _endOfStream = false;
        _repeatCount = 0;
        _repeatByte = -1;
        _bufferIndex = 0;
        _buffer = Array.Empty<byte>();
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
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (_endOfStream)
        {
            return 0;
        }

        int bytesRead = 0;
        while (bytesRead < count)
        {
            if (_repeatCount > 0)
            {
                buffer[offset + bytesRead] = (byte)_repeatByte;
                _repeatCount--;
                bytesRead++;
                continue;
            }

            if (_bufferIndex < _buffer.Length)
            {
                buffer[offset + bytesRead] = _buffer[_bufferIndex++];
                bytesRead++;
                continue;
            }

            int lengthByte = _baseStream.ReadByte();
            if (lengthByte == -1)
            {
                _endOfStream = true;
                break;
            }

            if (lengthByte == 128)
            {
                _endOfStream = true;
                break;
            }

            if (lengthByte < 128)
            {
                int dataLen = lengthByte + 1;
                _buffer = new byte[dataLen];
                int read = 0;
                while (read < dataLen)
                {
                    int b = _baseStream.ReadByte();
                    if (b == -1)
                    {
                        _endOfStream = true;
                        break;
                    }

                    _buffer[read++] = (byte)b;
                }

                _bufferIndex = 0;
                continue;
            }
            else if (lengthByte > 128)
            {
                _repeatCount = 257 - lengthByte;
                int b = _baseStream.ReadByte();
                if (b == -1)
                {
                    _endOfStream = true;
                    break;
                }

                _repeatByte = b;
                continue;
            }
        }

        return bytesRead;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _baseStream.Dispose();
        }

        base.Dispose(disposing);
    }
}
