using System;
using System.IO;

namespace PdfReader.Streams
{
    /// <summary>
    /// Forward-only stream that decodes PDF ASCII85Decode (ISO32000-1,7.4.3.5) on the fly.
    /// Rules:
    /// - Whitespace characters are ignored.
    /// - 'z' expands to four0x00 bytes (only valid when no other digits collected for the current group).
    /// - Data terminates at the sequence '~>' (EOD marker). The '>' is consumed.
    /// - Each5 base-85 digits (values0..84 mapped from '!'..'u') produce4 bytes (big-endian).
    /// - A final partial group of2..4 digits is padded with 'u' (value84) up to5 digits and only (digits -1) bytes are emitted.
    /// - A lone single digit at end (rare / malformed) is ignored (cannot form any output bytes).
    /// Robustness: invalid characters outside the permitted range are ignored.
    /// </summary>
    internal sealed class Ascii85DecodeStream : Stream
    {
        private readonly Stream _inner;
        private readonly bool _leaveOpen;

        // Decode state
        private bool _endReached;
        private readonly int[] _groupDigits = new int[5];
        private int _groupLength; //0..5 digits collected for current group

        // Output buffering (decoded bytes waiting to be consumed)
        private readonly byte[] _buffer = new byte[4];
        private int _bufferOffset;
        private int _bufferCount; // number of valid bytes in buffer

        public Ascii85DecodeStream(Stream inner, bool leaveOpen = false)
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
            // No-op.
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
                if (_bufferCount == 0)
                {
                    if (!FillDecodeBuffer())
                    {
                        break; // no more data
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

                // PDF whitespace characters
                if (IsWhiteSpace((byte)b))
                {
                    continue;
                }

                if (b == 'z')
                {
                    // Short form for0x00000000; only valid when starting a group.
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
                        // Ignore 'z' appearing mid-group (robustness); continue decoding digits already collected.
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
                    // Ignore invalid characters (robustness per spec recommendation)
                    continue;
                }

                int digit = b - '!'; //0..84
                _groupDigits[_groupLength] = digit;
                _groupLength++;

                if (_groupLength == 5)
                {
                    uint value = 0;
                    for (int i = 0; i < 5; i++)
                    {
                        value = value * 85 + (uint)_groupDigits[i];
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
                //0 or1 digits -> no output bytes.
                _groupLength = 0;
                return;
            }

            // Pad remaining digits with 'u' (value84) to make a full group.
            for (int i = _groupLength; i < 5; i++)
            {
                _groupDigits[i] = 84; // 'u'
            }

            uint value = 0;
            for (int i = 0; i < 5; i++)
            {
                value = value * 85 + (uint)_groupDigits[i];
            }

            // Emit only (groupLength -1) bytes.
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
