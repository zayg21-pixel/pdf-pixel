using System;
using System.IO;

namespace PdfRender.Streams
{
    /// <summary>
    /// Forward-only stream that decodes PDF ASCIIHexDecode (ISO 32000-1, 7.4.3.4) on the fly.
    /// - Ignores whitespace
    /// - Stops at the end-of-data marker '>' (0x3E)
    /// - If an odd number of hex digits is present, pads the last nibble with 0 to form the final byte
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

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException();
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
                        break; // only one padded byte can be returned
                    }

                    break;
                }

                // Ensure we have a high nibble
                if (_pendingHighNibble < 0)
                {
                    int high = ReadNextNibble();
                    if (high < 0)
                    {
                        // End reached (or no more nibble)
                        continue; // loop will handle flush/pad on next iteration
                    }

                    _pendingHighNibble = high;
                    continue; // try to get low nibble in the same loop iteration
                }

                int lowNibble = ReadNextNibble();
                if (lowNibble < 0)
                {
                    // End or EOD without low nibble; will be padded next iteration
                    continue;
                }

                // Emit a full byte
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

                // Ignore any other non-hex characters for robustness
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

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

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
