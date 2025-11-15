using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Jpg.Readers;

/// <summary>
/// Bit reader for JPEG entropy-coded segments. Handles 0xFF00 byte stuffing and exposes marker reading.
/// Marker reading and marker segment payload reading are separate atomic operations. Call <see cref="TryReadMarker"/>
/// to obtain the next marker code (if any) and then, for markers which include a length-prefixed payload, call
/// <see cref="ReadSegmentPayload"/> to obtain the payload bytes (excluding the two length bytes).
/// </summary>
internal ref struct JpgBitReader
{
    private const int MaxReservoirBits = 64;
    private const int ByteBitCount = 8;
    private const int ReservoirAppendThreshold = MaxReservoirBits - ByteBitCount; // 56 for 64-bit reservoir.
    private const byte MarkerPrefix = 0xFF;
    private const byte StuffingZero = 0x00;
    private const int MarkerSegmentLengthFieldSize = 2; // Length field includes its 2 bytes.

    private ReadOnlySpan<byte> _data;
    private int _pos;
    private int _remaining;
    private ulong _bitBuf;   // Left-aligned bit reservoir (high bits contain oldest bits). Up to 64 bits stored.
    private int _bits;        // Number of valid bits currently in _bitBuf (0..64).
    private bool _markerPending;

    /// <summary>
    /// Create a new bit reader over <paramref name="data"/> starting at position 0.
    /// </summary>
    /// <param name="data">Entropy-coded byte span (no ownership is taken).</param>
    public JpgBitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _remaining = data.Length;
        _pos = 0;
        _bitBuf = 0;
        _bits = 0;
        _markerPending = false;
    }

    /// <summary>
    /// Create a bit reader over <paramref name="data"/> resuming from a previously captured <paramref name="state"/>.
    /// </summary>
    public JpgBitReader(ReadOnlySpan<byte> data, JpgBitReaderState state)
    {
        _data = data;
        _remaining = data.Length - state.Pos;
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

        ulong bitBuffer = _bitBuf;
        int bufferedBits = _bits;

        if (!_markerPending)
        {
            while (bufferedBits < requiredBits && bufferedBits <= ReservoirAppendThreshold && _remaining > 0)
            {
                int reservoirBytes = ReservoirAppendThreshold - bufferedBits >> 3;
                int delta = reservoirBytes - _remaining;
                int mask = delta >> 31;
                int bytesToRead = _remaining + (delta & mask);

                if (bytesToRead >= 8)
                {
                    ulong chunk = BinaryPrimitives.ReadUInt64BigEndian(_data.Slice(_pos, 8));
                    const ulong HighBitMask = 0x8080808080808080UL;
                    const ulong ByteMask = 0x0101010101010101UL;
                    bool hasMarker = (chunk - ByteMask & ~chunk & HighBitMask) != 0;

                    if (!hasMarker)
                    {
                        bitBuffer = bitBuffer << ByteBitCount * 8 | chunk;
                        bufferedBits += ByteBitCount * 8;
                        _pos += 8;
                        _remaining -= 8;
                        continue;
                    }
                    // Marker found, process bytes up to marker using the chunk
                    for (int chunkIndex = 0; chunkIndex < 8; chunkIndex++)
                    {
                        int shift = (7 - chunkIndex) * ByteBitCount;
                        byte value = (byte)(chunk >> shift & 0xFF);
                        if (value == MarkerPrefix)
                        {
                            break;
                        }
                        bitBuffer = bitBuffer << ByteBitCount | value;
                        bufferedBits += ByteBitCount;
                        _pos++;
                        _remaining--;
                    }
                    // Now process marker byte as usual below
                }
                // Byte-by-byte fallback (marker or not enough for chunk)
                byte valueByte = _data[_pos];
                if (valueByte != MarkerPrefix)
                {
                    _pos++;
                    _remaining--;
                    bitBuffer = bitBuffer << ByteBitCount | valueByte;
                    bufferedBits += ByteBitCount;
                    continue;
                }
                if (_remaining >= MarkerSegmentLengthFieldSize)
                {
                    byte next = _data[_pos + 1];
                    if (next == StuffingZero)
                    {
                        _pos += MarkerSegmentLengthFieldSize;
                        _remaining -= MarkerSegmentLengthFieldSize;
                        bitBuffer = bitBuffer << ByteBitCount | MarkerPrefix;
                        bufferedBits += ByteBitCount;
                        continue;
                    }
                    _markerPending = true;
                    break;
                }
                else
                {
                    _pos++;
                    _remaining--;
                    bitBuffer = bitBuffer << ByteBitCount | MarkerPrefix;
                    bufferedBits += ByteBitCount;
                    continue;
                }
            }
        }

        // Pad with zero bits if blocked by marker or end-of-data and still need bits.
        if (bufferedBits < requiredBits && (_markerPending || _remaining == 0))
        {
            int neededBits = requiredBits - bufferedBits;
            int availableBits = MaxReservoirBits - bufferedBits;
            if (neededBits > availableBits)
            {
                neededBits = availableBits;
            }
            int padBits = neededBits + (ByteBitCount - 1) & ~(ByteBitCount - 1);
            if (padBits > 0)
            {
                bitBuffer <<= padBits;
                bufferedBits += padBits;
            }
        }

        _bitBuf = bitBuffer;
        _bits = bufferedBits;
    }

    /// <summary>
    /// Peek 16 bits (without consuming) for slow-path Huffman decoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint PeekBits16()
    {
        EnsureBits(16);
        int shift = _bits - 16;
        return (uint)(_bitBuf >> shift & 0xFFFFUL);
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
        uint value = (uint)(_bitBuf >> newBits & (1UL << bitCount) - 1UL);
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
        int signBit = 1 << bitCount - 1;
        int value = raw;
        int adjustMask = value - signBit >> 31;
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

        int markerPos = _pos;
        int markerRemaining = _remaining;

        if (_data[markerPos] != MarkerPrefix)
        {
            return false;
        }
        markerPos++;
        markerRemaining--;

        while (markerRemaining > 0 && _data[markerPos] == MarkerPrefix)
        {
            markerPos++;
            markerRemaining--;
        }

        if (markerRemaining <= 0)
        {
            return false;
        }

        byte code = _data[markerPos];
        markerPos++;
        markerRemaining--;
        if (code == StuffingZero)
        {
            return false;
        }

        int consumed = markerPos - _pos;
        _pos = markerPos;
        _remaining = markerRemaining;
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

        int lenHigh = _data[_pos];
        int lenLow = _data[_pos + 1];
        int segmentLength = lenHigh << 8 | lenLow;
        if (segmentLength < MarkerSegmentLengthFieldSize)
        {
            throw new InvalidOperationException("Invalid marker segment length.");
        }
        if (_remaining < segmentLength)
        {
            throw new InvalidOperationException("Truncated marker segment payload.");
        }

        int payloadLength = segmentLength - MarkerSegmentLengthFieldSize;
        int payloadStart = _pos + MarkerSegmentLengthFieldSize;
        ReadOnlySpan<byte> payload = _data.Slice(payloadStart, payloadLength);

        _pos += segmentLength;
        _remaining -= segmentLength;
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
