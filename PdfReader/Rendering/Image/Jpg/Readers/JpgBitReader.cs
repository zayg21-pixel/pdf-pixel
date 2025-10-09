using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Jpg.Readers
{
    /// <summary>
    /// Bit reader for JPEG entropy-coded segments. Handles 0xFF00 byte stuffing and exposes marker reading.
    /// Marker reading and marker segment payload reading are separate atomic operations. Call <see cref="TryReadMarker"/>
    /// to obtain the next marker code (if any) and then, for markers which include a length-prefixed payload, call
    /// <see cref="ReadSegmentPayload"/> to obtain the payload bytes (excluding the two length bytes).
    /// </summary>
    internal unsafe ref struct JpgBitReader
    {
        private const int MaxReservoirBits = 64;
        private const int ByteBitCount = 8;
        private const int ReservoirAppendThreshold = MaxReservoirBits - ByteBitCount; // 56 for 64-bit reservoir.
        private const byte MarkerPrefix = 0xFF;
        private const byte StuffingZero = 0x00;
        private const int MarkerSegmentLengthFieldSize = 2; // Length field includes its 2 bytes.

        private byte* _current;
        private int _remaining;
        private int _pos;
        private ulong _bitBuf;   // Left-aligned bit reservoir (high bits contain oldest bits). Up to 64 bits stored.
        private int _bits;        // Number of valid bits currently in _bitBuf (0..64).
        private bool _markerPending;

        /// <summary>
        /// Create a new bit reader over <paramref name="data"/> starting at position 0.
        /// </summary>
        /// <param name="data">Entropy-coded byte span (no ownership is taken).</param>
        public JpgBitReader(ref ReadOnlySpan<byte> data)
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
        /// Create a bit reader over <paramref name="data"/> resuming from a previously captured <paramref name="state"/>.
        /// </summary>
        public JpgBitReader(ref ReadOnlySpan<byte> data, JpgBitReaderState state)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureBits(int requiredBits)
        {
            if (_bits >= requiredBits)
            {
                return;
            }

            ulong bitBuf = _bitBuf;
            int bits = _bits;

            if (!_markerPending)
            {
                while (bits < requiredBits && bits <= ReservoirAppendThreshold && _remaining > 0)
                {
                    byte value = *_current;

                    if (value != MarkerPrefix)
                    {
                        _current++;
                        _remaining--;
                        _pos++;
                        bitBuf = (bitBuf << ByteBitCount) | value;
                        bits += ByteBitCount;
                        continue;
                    }

                    if (_remaining >= MarkerSegmentLengthFieldSize)
                    {
                        byte next = _current[1];
                        if (next == StuffingZero)
                        {
                            _current += MarkerSegmentLengthFieldSize;
                            _remaining -= MarkerSegmentLengthFieldSize;
                            _pos += MarkerSegmentLengthFieldSize;
                            bitBuf = (bitBuf << ByteBitCount) | MarkerPrefix;
                            bits += ByteBitCount;
                            continue;
                        }

                        _markerPending = true;
                        break;
                    }
                    else
                    {
                        _current++;
                        _remaining--;
                        _pos++;
                        bitBuf = (bitBuf << ByteBitCount) | MarkerPrefix;
                        bits += ByteBitCount;
                        continue;
                    }
                }
            }

            // Pad with zero bits if blocked by marker or end-of-data and still need bits.
            if (bits < requiredBits && (_markerPending || _remaining == 0))
            {
                int neededBits = requiredBits - bits;
                // Limit to remaining reservoir capacity.
                int availableBits = MaxReservoirBits - bits; // Always multiple of 8.
                if (neededBits > availableBits)
                {
                    neededBits = availableBits;
                }
                // Round up to next whole byte (multiple of 8) using mask instead of divide/multiply.
                int padBits = (neededBits + (ByteBitCount - 1)) & ~(ByteBitCount - 1);
                if (padBits > 0)
                {
                    bitBuf <<= padBits;
                    bits += padBits;
                }
            }

            _bitBuf = bitBuf;
            _bits = bits;
        }

        /// <summary>
        /// Peek 16 bits (without consuming) for slow-path Huffman decoding.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint PeekBits16()
        {
            EnsureBits(16);
            int shift = _bits - 16;
            return (uint)((_bitBuf >> shift) & 0xFFFFUL);
        }

        /// <summary>
        /// Drop (discard) <paramref name="bitCount"/> previously peeked bits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DropBits(int bitCount)
        {
            _bits -= bitCount;
        }

        /// <summary>
        /// Read (and consume) up to 16 bits. Returns 0 if <paramref name="bitCount"/> is 0.
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
            uint value = (uint)((_bitBuf >> newBits) & ((1UL << bitCount) - 1UL));
            _bits = newBits;
            return value;
        }

        /// <summary>
        /// Read a JPEG signed value with the given bit length where the top bit is the sign indicator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadSigned(int bitCount)
        {
            if (bitCount == 0)
            {
                return 0;
            }
            EnsureBits(bitCount);
            int mask = (1 << bitCount) - 1;
            int newBits = _bits - bitCount;
            int raw = (int)(_bitBuf >> newBits) & mask;
            _bits = newBits;
            int signBit = 1 << (bitCount - 1);
            int value = raw;
            int adjustMask = (value - signBit) >> 31;
            value -= adjustMask & mask;
            return value;
        }

        /// <summary>
        /// Discard any buffered bits and align to the next byte boundary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ByteAlign()
        {
            _bitBuf = 0;
            _bits = 0;
        }

        /// <summary>
        /// Attempt to read the next marker. Returns false if no marker is present at the current position.
        /// On success the internal state is byte-aligned at the first byte after the marker code.
        /// Stand-alone markers (SOI/EOI/RSTn/TEM) are treated the same as other markers here; payload handling is separate.
        /// </summary>
        /// <param name="marker">Marker code (low byte) when return value is true.</param>
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

            if (*markerPtr++ != MarkerPrefix)
            {
                return false;
            }
            markerRemaining--;

            while (markerRemaining > 0 && *markerPtr == MarkerPrefix)
            {
                markerPtr++;
                markerRemaining--;
            }

            if (markerRemaining <= 0)
            {
                return false;
            }

            byte code = *markerPtr++;
            if (code == StuffingZero)
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
        /// Read and return the payload bytes for a marker whose payload is known to exist (i.e. not SOI, EOI, RSTn, TEM).
        /// The payload length is determined by the 2-byte big-endian length value (which includes those 2 bytes).
        /// The returned span excludes the two length bytes. The reader advances past the entire segment.
        /// </summary>
        /// <returns>Payload bytes (may be empty if length == 2).</returns>
        /// <exception cref="InvalidOperationException">Thrown on truncated or invalid length.</exception>
        public ReadOnlySpan<byte> ReadSegmentPayload()
        {
            if (_remaining < MarkerSegmentLengthFieldSize)
            {
                throw new InvalidOperationException("Truncated marker segment length.");
            }

            int lenHigh = _current[0];
            int lenLow = _current[1];
            int segmentLength = (lenHigh << 8) | lenLow;
            if (segmentLength < MarkerSegmentLengthFieldSize)
            {
                throw new InvalidOperationException("Invalid marker segment length.");
            }
            if (_remaining < segmentLength)
            {
                throw new InvalidOperationException("Truncated marker segment payload.");
            }

            int payloadLength = segmentLength - MarkerSegmentLengthFieldSize;
            byte* payloadPtr = _current + MarkerSegmentLengthFieldSize;
            ReadOnlySpan<byte> payload = new ReadOnlySpan<byte>(payloadPtr, payloadLength);

            _current += segmentLength;
            _remaining -= segmentLength;
            _pos += segmentLength;
            return payload;
        }

        /// <summary>
        /// Capture a snapshot of the current position and buffered bits for later resumption.
        /// </summary>
        public JpgBitReaderState CaptureState()
        {
            return new JpgBitReaderState(_pos, _bitBuf, _bits, _markerPending);
        }
    }

    /// <summary>
    /// Serializable snapshot of a <see cref="JpgBitReader"/> internal state.
    /// </summary>
    internal readonly struct JpgBitReaderState
    {
        /// <summary>
        /// Byte position (number of source bytes consumed).
        /// </summary>
        public readonly int Pos;
        /// <summary>
        /// Buffered bits reservoir.
        /// </summary>
        public readonly ulong BitBuf;
        /// <summary>
        /// Count of valid bits currently in <see cref="BitBuf"/>.
        /// </summary>
        public readonly int Bits;
        /// <summary>
        /// True if a marker prefix (0xFF) was encountered and pending marker consumption prevented further byte fetch.
        /// </summary>
        public readonly bool MarkerPending;

        /// <summary>
        /// Initialize a new snapshot instance.
        /// </summary>
        public JpgBitReaderState(int pos, ulong bitBuf, int bits, bool markerPending)
        {
            Pos = pos;
            BitBuf = bitBuf;
            Bits = bits;
            MarkerPending = markerPending;
        }
    }
}
