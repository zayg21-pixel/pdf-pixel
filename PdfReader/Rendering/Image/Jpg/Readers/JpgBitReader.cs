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
        private static uint Mask(int bitCount)
        {
            if (bitCount == 0)
            {
                return 0u;
            }
            return (uint)((1u << bitCount) - 1u);
        }

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

                byte value = *_current;
                _current++;
                _remaining--;
                _pos++;
                if (value == 0xFF)
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

                _bitBuf = (_bitBuf << 8) | value;
                _bits += 8;
            }
        }

        /// <summary>
        /// Generic peek for n bits (1..16). Retains high-bit buffer orientation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint PeekBits(int bitCount)
        {
            EnsureBits(bitCount);
            return _bitBuf >> (_bits - bitCount) & Mask(bitCount);
        }

        /// <summary>
        /// Specialized 8-bit peek (hot path for Huffman lookahead).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint PeekBits8()
        {
            EnsureBits(8);
            return (_bitBuf >> (_bits - 8)) & 0xFFu;
        }

        /// <summary>
        /// Specialized 16-bit peek (used for extended Huffman decode slow path).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint PeekBits16()
        {
            EnsureBits(16);
            return (_bitBuf >> (_bits - 16)) & 0xFFFFu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DropBits(int bitCount)
        {
            if (bitCount < 0 || bitCount > _bits)
            {
                bitCount = _bits;
            }

            _bits -= bitCount;
        }

        /// <summary>
        /// Read n bits (0..16) in a single combined Ensure/extract/drop step to avoid separate Peek + Drop overhead.
        /// Returns 0 if bitCount is 0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadBits(int bitCount)
        {
            if (bitCount == 0)
            {
                return 0u;
            }

            EnsureBits(bitCount);
            int newBits = _bits - bitCount;
            uint value = (_bitBuf >> newBits) & Mask(bitCount);
            _bits = newBits;
            return value;
        }

        /// <summary>
        /// Specialized 8-bit read (hot path) combining ensure, extract, and drop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadBits8()
        {
            EnsureBits(8);
            int newBits = _bits - 8;
            uint value = (_bitBuf >> newBits) & 0xFFu;
            _bits = newBits;
            return value;
        }

        /// <summary>
        /// Specialized 16-bit read combining ensure, extract, and drop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadBits16()
        {
            EnsureBits(16);
            int newBits = _bits - 16;
            uint value = (_bitBuf >> newBits) & 0xFFFFu;
            _bits = newBits;
            return value;
        }

        /// <summary>
        /// Read a JPEG signed value encoded with <paramref name="bitCount"/> bits where the top bit indicates sign.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadSigned(int bitCount)
        {
            if (bitCount == 0)
            {
                return 0;
            }

            uint raw = ReadBits(bitCount);
            int threshold = 1 << (bitCount - 1);
            int fullMask = (1 << bitCount) - 1;
            int negativeMask = ((int)raw - threshold) >> 31;
            int result = (int)raw - (negativeMask & fullMask);
            return result;
        }

        /// <summary>
        /// Align to next byte boundary by discarding any buffered bits from the bit buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ByteAlign()
        {
            _bitBuf = 0;
            _bits = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadMarker(out byte marker)
        {
            marker = 0;
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
                return false;
            }

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
