using System;
using System.IO;

namespace PdfRender.Streams
{
    /// <summary>
    /// Stream for decoding PDF RunLengthDecode filter.
    /// Implements the PDF spec: each data block ends with128 (0x80),
    /// followed by a single byte (0x80) to mark EOD.
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

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
