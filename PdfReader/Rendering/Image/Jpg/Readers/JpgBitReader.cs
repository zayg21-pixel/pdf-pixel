using System;

namespace PdfReader.Rendering.Image.Jpg.Readers
{
    /// <summary>
    /// Bit reader for JPEG entropy-coded segments. Handles 0xFF00 byte stuffing and exposes marker reading.
    /// The reader avoids consuming marker bytes; callers can detect and read markers between MCUs.
    /// </summary>
    internal ref struct JpgBitReader
    {
        private ReadOnlySpan<byte> _data;
        private int _pos;
        private uint _bitBuf;
        private int _bits;
        private bool _markerPending;

        public JpgBitReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _pos = 0;
            _bitBuf = 0;
            _bits = 0;
            _markerPending = false;
        }

        /// <summary>
        /// Create a bit reader with a previously captured state to resume decoding.
        /// </summary>
        public JpgBitReader(ReadOnlySpan<byte> data, JpgBitReaderState state)
        {
            _data = data;
            _pos = state.Pos;
            _bitBuf = state.BitBuf;
            _bits = state.Bits;
            _markerPending = state.MarkerPending;
        }

        public int Position => _pos * 8 - _bits; // Return bit position, not byte position

        public void EnsureBits(int requiredBits)
        {
            while (_bits < requiredBits)
            {
                if (_markerPending)
                {
                    _bitBuf <<= 8;
                    _bits += 8;
                    continue;
                }

                if (_pos >= _data.Length)
                {
                    _bitBuf <<= 8;
                    _bits += 8;
                    continue;
                }

                byte b = _data[_pos++];
                if (b == 0xFF)
                {
                    if (_pos < _data.Length)
                    {
                        byte next = _data[_pos];
                        if (next == 0x00)
                        {
                            _pos++;
                        }
                        else
                        {
                            _pos--;
                            _markerPending = true;
                            continue;
                        }
                    }
                }

                _bitBuf = _bitBuf << 8 | b;
                _bits += 8;
            }
        }

        public uint PeekBits(int n)
        {
            EnsureBits(n);
            return _bitBuf >> _bits - n & (uint)(1 << n) - 1;
        }

        public void DropBits(int n)
        {
            if (n < 0 || n > _bits)
            {
                n = _bits;
            }

            _bits -= n;
        }

        public uint ReadBits(int n)
        {
            uint v = PeekBits(n);
            DropBits(n);
            return v;
        }

        public int ReadSigned(int n)
        {
            if (n == 0)
            {
                return 0;
            }

            uint v = ReadBits(n);
            int vt = 1 << n - 1;
            if (v < (uint)vt)
            {
                return (int)v - ((1 << n) - 1);
            }

            return (int)v;
        }

        /// <summary>
        /// Align to next byte boundary by discarding any buffered bits from the bit buffer.
        /// This fully flushes the buffer so the next read is at a byte boundary in the underlying stream.
        /// </summary>
        public void ByteAlign()
        {
            _bitBuf = 0;
            _bits = 0;
        }

        public bool TryReadMarker(out byte marker)
        {
            marker = 0;

            // Ensure we're at a byte boundary and bit buffer flushed
            ByteAlign();

            if (_pos >= _data.Length)
            {
                return false;
            }

            // Expect one or more 0xFF fill bytes, then a non-0xFF marker code
            int i = _pos;
            if (_data[i++] != 0xFF)
            {
                return false;
            }

            while (i < _data.Length && _data[i] == 0xFF)
            {
                i++;
            }

            if (i >= _data.Length)
            {
                return false;
            }

            byte code = _data[i++];
            if (code == 0x00)
            {
                // Stuffed 0x00 means 0xFF00 was a data byte sequence, not a marker.
                return false;
            }

            // Consume the marker bytes
            _pos = i;
            _markerPending = false;
            _bitBuf = 0;
            _bits = 0;
            marker = code;
            return true;
        }

        /// <summary>
        /// Capture the current internal state to resume later on a new bit reader instance.
        /// </summary>
        public JpgBitReaderState CaptureState()
        {
            return new JpgBitReaderState(_pos, _bitBuf, _bits, _markerPending);
        }
    }

    /// <summary>
    /// Serializable snapshot of a JpgBitReader position and buffered bits.
    /// </summary>
    internal readonly struct JpgBitReaderState
    {
        public readonly int Pos;
        public readonly uint BitBuf;
        public readonly int Bits;
        public readonly bool MarkerPending;

        public JpgBitReaderState(int pos, uint bitBuf, int bits, bool markerPending)
        {
            Pos = pos;
            BitBuf = bitBuf;
            Bits = bits;
            MarkerPending = markerPending;
        }
    }
}
