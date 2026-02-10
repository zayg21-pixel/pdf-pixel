using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Reads arbitrary bit-width values from a byte array, supporting up to 32 bits at a time.
/// Uses an internal buffer for efficient bit extraction.
/// </summary>
internal ref struct UintBitReader
{
    private ReadOnlySpan<byte> _data;
    private int _bitPosition;
    private int _bitLength;

    // Internal buffer for up to 64 bits
    private ulong _buffer;
    private int _bufferedBits;
    private int _bufferedByteIndex;

    public UintBitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _bitLength = data.Length * 8;
        _bitPosition = 0;
        _buffer = 0;
        _bufferedBits = 0;
        _bufferedByteIndex = 0;
    }

    /// <summary>
    /// Gets a value indicating whether the end of the data has been reached.
    /// </summary>
    public bool EndOfData => _bitPosition >= _bitLength;

    /// <summary>
    /// Gets the current bit position in the stream.
    /// </summary>
    public int BitPosition => _bitPosition;

    /// <summary>
    /// Gets a value indicating whether the reader is currently aligned to a byte boundary.
    /// </summary>
    public bool IsByteAligned => _bitPosition % 8 == 0;

    /// <summary>
    /// Reads the specified number of bits and returns the value as an unsigned integer.
    /// </summary>
    /// <param name="count">Number of bits to read (1-32).</param>
    /// <returns>Unsigned integer value of the read bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadBits(int count)
    {
        if (count < 1 || count > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Bit count must be between 1 and 32.");
        }

        if (_bitPosition + count > _bitLength)
        {
            // Not enough bits left; return 0
            return 0;
        }

        // Fill buffer if not enough bits are available
        if (_bufferedBits < count)
        {
            FillBuffer();
        }

        // Extract bits from buffer
        int shift = _bufferedBits - count;
        uint mask = (uint)((1UL << count) - 1);
        uint value = (uint)((_buffer >> shift) & mask);

        _bufferedBits -= count;
        _buffer &= (1UL << _bufferedBits) - 1; // Remove consumed bits
        _bitPosition += count;

        return value;
    }

    /// <summary>
    /// Fills the internal buffer with up to 64 bits from the data span using BinaryPrimitives for efficiency.
    /// Uses a branchless bit trick for min calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FillBuffer()
    {
        const int MaxBufferBits = 64;
        int bitsRemaining = MaxBufferBits - _bufferedBits;
        int bytesRemaining = _data.Length - _bufferedByteIndex;

        // Use bit shifting instead of division by 8
        int bitsRemainingBytes = bitsRemaining >> 3;

        // Branchless min: bytesToRead = min(bytesRemaining, bitsRemainingBytes)
        // mask is 0 if bitsRemainingBytes >= bytesRemaining, -1 otherwise.
        int delta = bitsRemainingBytes - bytesRemaining;
        int mask = delta >> 31;
        int bytesToRead = bytesRemaining + (delta & mask);

        if (bytesToRead >= 8)
        {
            // Use BinaryPrimitives to read 8 bytes at once
            ulong chunk = BinaryPrimitives.ReadUInt64BigEndian(_data.Slice(_bufferedByteIndex, 8));
            _buffer = (_buffer << 64) | chunk;
            _bufferedByteIndex += 8;
            _bufferedBits += 64;
        }
        else
        {
            // Manual byte-by-byte loading
            int endIndex = _bufferedByteIndex + bytesToRead;
            for (int byteIndex = _bufferedByteIndex; byteIndex < endIndex; byteIndex++)
            {
                _buffer = (_buffer << 8) | _data[byteIndex];
                _bufferedBits += 8;
            }
            _bufferedByteIndex = endIndex;
        }
    }
}
