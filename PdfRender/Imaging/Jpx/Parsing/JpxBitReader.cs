using System;
using System.Runtime.CompilerServices;

namespace PdfRender.Imaging.Jpx.Parsing;

/// <summary>
/// Bit reader for JPEG 2000 (JPX) entropy-coded packet data.
/// Handles MSB-first bit reading for packet headers and tag-tree decoding.
/// </summary>
internal ref struct JpxBitReader
{
    private const int MaxReservoirBits = 64;
    private const int ByteBitCount = 8;
    private const int ReservoirAppendThreshold = MaxReservoirBits - ByteBitCount; // 56 for 64-bit reservoir

    private ReadOnlySpan<byte> _data;
    private int _pos;
    private int _remaining;
    private ulong _bitBuf;   // Left-aligned bit reservoir (high bits contain oldest bits). Up to 64 bits stored.
    private int _bits;       // Number of valid bits currently in _bitBuf (0..64).

    /// <summary>
    /// Creates a new bit reader over the specified data starting at position 0.
    /// </summary>
    /// <param name="data">Packet data span (no ownership is taken).</param>
    public JpxBitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _remaining = data.Length;
        _pos = 0;
        _bitBuf = 0;
        _bits = 0;
    }

    /// <summary>
    /// Gets the current byte position within the data.
    /// </summary>
    public int Position => _pos;

    /// <summary>
    /// Gets the remaining bytes in the data.
    /// </summary>
    public int Remaining => _remaining;

    /// <summary>
    /// Gets the total number of bits consumed so far.
    /// </summary>
    public int BitsConsumed => _pos * ByteBitCount - _bits;

    /// <summary>
    /// Ensures at least the requested number of bits are available in the bit buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureBits(int requiredBits)
    {
        if (_bits >= requiredBits)
        {
            return;
        }

        ulong bitBuffer = _bitBuf;
        int bufferedBits = _bits;

        while (bufferedBits < requiredBits && bufferedBits <= ReservoirAppendThreshold && _remaining > 0)
        {
            byte valueByte = _data[_pos];
            _pos++;
            _remaining--;
            bitBuffer = bitBuffer << ByteBitCount | valueByte;
            bufferedBits += ByteBitCount;
        }

        // Pad with zero bits if we reach end-of-data and still need more bits
        if (bufferedBits < requiredBits && _remaining == 0)
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
    /// Peeks at the next bit without consuming it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PeekBit()
    {
        EnsureBits(1);
        if (_bits == 0)
        {
            return 0; // EOF
        }
        return (int)(_bitBuf >> (_bits - 1)) & 1;
    }

    /// <summary>
    /// Reads a single bit from the current position.
    /// </summary>
    /// <returns>0 or 1.</returns>
    public int ReadBit()
    {
        EnsureBits(1);
        if (_bits == 0)
        {
            return 0; // EOF
        }
        int bit = (int)(_bitBuf >> (_bits - 1)) & 1;
        _bits--;
        return bit;
    }

    /// <summary>
    /// Reads the specified number of bits from the current position.
    /// </summary>
    /// <param name="count">Number of bits to read (1-32).</param>
    /// <returns>The bits as an unsigned integer, MSB first.</returns>
    public uint ReadBits(int count)
    {
        if (count <= 0 || count > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 32.");
        }

        uint result = 0;
        for (int i = 0; i < count; i++)
        {
            result = (result << 1) | (uint)ReadBit();
        }

        return result;
    }

    /// <summary>
    /// Aligns to the next byte boundary by discarding any remaining bits in the current byte.
    /// </summary>
    public void ByteAlign()
    {
        int bitsToDiscard = _bits % ByteBitCount;
        if (bitsToDiscard > 0)
        {
            _bits -= bitsToDiscard;
        }
    }

    /// <summary>
    /// Skips the specified number of bits.
    /// </summary>
    public void SkipBits(int bitCount)
    {
        while (bitCount > 0)
        {
            int toSkip = Math.Min(bitCount, 32);
            ReadBits(toSkip);
            bitCount -= toSkip;
        }
    }

    /// <summary>
    /// Checks if more data is available for reading.
    /// </summary>
    public bool HasMoreData => _bits > 0 || _remaining > 0;
}