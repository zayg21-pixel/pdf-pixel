using System;
using System.IO;

namespace PdfPixel.Streams
{
    /// <summary>
    /// Forward-only stream that decodes PDF ASCIIHexDecode (ISO 32000-1, 7.4.3.4).
    /// </summary>
    internal sealed class AsciiHexDecodeStream : Stream
    {
        private readonly Stream _inner;
        private readonly bool _leaveOpen;
        private bool _endReached;
        private int _pendingHighNibble = -1; // -1 => none, 0..15 => value
        private bool _oddNibblePaddedReturned;

        public AsciiHexDecodeStream(Stream inner, bool leaveOpen = false)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (!inner.CanRead)
            {
                throw new ArgumentException("Inner stream must be readable", nameof(inner));
            }

            _leaveOpen = leaveOpen;
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

        public override void Flush()
        {
            // no-op
        }

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
                if (_endReached)
                {
                    if (_pendingHighNibble >= 0 && !_oddNibblePaddedReturned)
                    {
                        buffer[offset + written] = (byte)((_pendingHighNibble << 4) & 0xF0);
                        _pendingHighNibble = -1;
                        _oddNibblePaddedReturned = true;
                        written++;
                        break;
                    }

                    break;
                }

                if (_pendingHighNibble < 0)
                {
                    int high = ReadNextNibble();
                    if (high < 0)
                    {
                        continue;
                    }

                    _pendingHighNibble = high;
                    continue;
                }

                int lowNibble = ReadNextNibble();
                if (lowNibble < 0)
                {
                    continue;
                }

                buffer[offset + written] = (byte)((_pendingHighNibble << 4) | lowNibble);
                _pendingHighNibble = -1;
                written++;
            }

            return written;
        }

        private int ReadNextNibble()
        {
            while (true)
            {
                int b = _inner.ReadByte();
                if (b < 0)
                {
                    _endReached = true;
                    return -1;
                }

                if (b == '>')
                {
                    _endReached = true;
                    return -1;
                }

                if (IsWhiteSpace((byte)b))
                {
                    continue;
                }

                int nibble = HexValueOrMinusOne((byte)b);
                if (nibble >= 0)
                {
                    return nibble;
                }
            }
        }

        private static bool IsWhiteSpace(byte b)
        {
            // PDF whitespace: NUL (0), HT (9), LF (10), FF (12), CR (13), Space (32)
            return b == 0 || b == 9 || b == 10 || b == 12 || b == 13 || b == 32;
        }

        private static int HexValueOrMinusOne(byte b)
        {
            if (b >= (byte)'0' && b <= (byte)'9')
            {
                return b - (byte)'0';
            }

            if (b >= (byte)'A' && b <= (byte)'F')
            {
                return b - (byte)'A' + 10;
            }

            if (b >= (byte)'a' && b <= (byte)'f')
            {
                return b - (byte)'a' + 10;
            }

            return -1;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
