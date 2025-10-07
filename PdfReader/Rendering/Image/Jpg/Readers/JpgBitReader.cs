using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Jpg.Readers
{
    /// <summary>
    /// Bit reader for JPEG entropy-coded segments. Handles 0xFF00 byte stuffing and exposes marker reading.
    /// The reader avoids consuming marker bytes; callers can detect and read markers between MCUs.
    /// </summary>
    internal unsafe ref struct JpgBitReader
    {
        private byte* _current;
        private int _remaining;
        private int _pos;
        private uint _bitBuf;
        private int _bits;
        private bool _markerPending;

        public JpgBitReader(ReadOnlySpan<byte> data)
        {
            fixed (byte* dataPtr = data)
            {
                _current = dataPtr;
                _remaining = data.Length;
            }
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
            fixed (byte* dataPtr = data)
            {
                _current = dataPtr + state.Pos;
                _remaining = data.Length - state.Pos;
            }
            _pos = state.Pos;
            _bitBuf = state.BitBuf;
            _bits = state.Bits;
            _markerPending = state.MarkerPending;
        }

        public int Position => _pos * 8 - _bits;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureBits(int requiredBits)
        {
            while (_bits < requiredBits)
            {
                if (_markerPending)
                {
                    _bitBuf <<= 8;
                    _bits += 8;
                    continue;
                }

                if (_remaining <= 0)
                {
                    _bitBuf <<= 8;
                    _bits += 8;
                    continue;
                }

                byte b = *_current;
                _current++;
                _remaining--;
                _pos++;
                if (b == 0xFF)
                {
                    if (_remaining > 0)
                    {
                        byte next = *_current;
                        if (next == 0x00)
                        {
                            _current++;
                            _remaining--;
                            _pos++;
                        }
                        else
                        {
                            _current--;
                            _remaining++;
                            _pos--;
                            _markerPending = true;
                            continue;
                        }
                    }
                }

                _bitBuf = (_bitBuf << 8) | b;
                _bits += 8;
            }
        }

        public uint PeekBits(int n)
        {
            EnsureBits(n);
            return _bitBuf >> (_bits - n) & (uint)((1 << n) - 1);
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
            int vt = 1 << (n - 1);
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

            if (_remaining <= 0)
            {
                return false;
            }

            byte* markerPtr = _current;
            int markerRemaining = _remaining;
            int markerPos = _pos;
            if (*markerPtr++ != 0xFF)
            {
                return false;
            }
            markerRemaining--;
            markerPos++;

            while (markerRemaining > 0 && *markerPtr == 0xFF)
            {
                markerPtr++;
                markerRemaining--;
                markerPos++;
            }

            if (markerRemaining <= 0)
            {
                return false;
            }

            byte code = *markerPtr++;
            if (code == 0x00)
            {
                // Stuffed 0x00 means 0xFF00 was a data byte sequence, not a marker.
                return false;
            }

            // Consume the marker bytes
            int consumed = (int)(markerPtr - _current);
            _current = markerPtr;
            _remaining -= consumed;
            _pos += consumed;
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
