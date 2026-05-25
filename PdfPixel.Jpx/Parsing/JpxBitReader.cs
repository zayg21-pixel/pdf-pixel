using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Bit reader for JPEG 2000 (JPX) entropy-coded packet data.
/// Handles MSB-first bit reading with bit-stuffing per ITU-T T.800 B.10.1:
/// after a 0xFF byte, the next byte's MSB is a stuffing bit (discarded, only 7 bits valid).
/// </summary>
internal ref struct JpxBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _pos;
    private int _remaining;
    private int _currentByte;  // Current byte buffer (-1 means empty)
    private int _bitsLeft;     // Number of valid bits remaining in _currentByte (0..8)
    private int _bitsConsumed; // Total bits consumed so far

    /// <summary>
    /// Creates a new bit reader over the specified data starting at position 0.
    /// </summary>
    /// <param name="data">Packet data span (no ownership is taken).</param>
    public JpxBitReader(in ReadOnlySpan<byte> data)
    {
        _data = data;
        _remaining = data.Length;
        _pos = 0;
        _currentByte = 0;
        _bitsLeft = 0;
        _bitsConsumed = 0;
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
    public int BitsConsumed => _bitsConsumed;

    /// <summary>
    /// Loads the next byte, applying bit-stuffing rules.
    /// After a 0xFF byte, the next byte only has 7 valid bits (MSB is stuffing bit).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LoadNextByte()
    {
        bool previousWasFF = (_currentByte == 0xFF);

        if (_remaining > 0)
        {
            _currentByte = _data[_pos];
            _pos++;
            _remaining--;
        }
        else
        {
            _currentByte = 0; // Pad with zeros at end of data
        }

        // Per ITU-T T.800 B.10.1: after 0xFF, next byte has only 7 valid bits
        _bitsLeft = previousWasFF ? 7 : 8;
    }

    /// <summary>
    /// Reads a single bit from the current position.
    /// </summary>
    /// <returns>0 or 1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadBit()
    {
        if (_bitsLeft == 0)
        {
            LoadNextByte();
        }

        _bitsLeft--;
        _bitsConsumed++;
        return (_currentByte >> _bitsLeft) & 1;
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
        if (_bitsLeft > 0)
        {
            _bitsConsumed += _bitsLeft;
            _bitsLeft = 0;
        }
    }

    /// <summary>
    /// Skips the specified number of bits.
    /// </summary>
    public void SkipBits(int bitCount)
    {
        while (bitCount > 0)
        {
            ReadBit();
            bitCount--;
        }
    }

    /// <summary>
    /// Checks if more data is available for reading.
    /// </summary>
    public bool HasMoreData => _bitsLeft > 0 || _remaining > 0;


    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> slice of the underlying data without copying.
    /// Must be called after <see cref="ByteAlign"/> (i.e., when no partial byte is buffered).
    /// Advances the position by <paramref name="count"/> bytes.
    /// </summary>
    /// <param name="count">Number of bytes to read.</param>
    /// <returns>A span over the raw data bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> ReadRawSpan(int count)
    {
        int available = Math.Min(count, _remaining);
        ReadOnlySpan<byte> span = _data.Slice(_pos, available);
        _pos += available;
        _remaining -= available;

        // Reset bit state
        _currentByte = 0;
        _bitsLeft = 0;
        _bitsConsumed += count * 8;

        return span;
    }
}
