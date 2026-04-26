using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Reads arbitrary bit-width values from a byte array, supporting up to 32 bits at a time.
/// Uses an MSB-aligned internal buffer for efficient bit extraction.
/// </summary>
internal ref struct UintBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _bitPosition;
    private int _bitLength;

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

    public bool EndOfData => _bitPosition >= _bitLength;
    public int BitPosition => _bitPosition;

    /// <summary>
    /// Reads the specified number of bits and returns the value as an unsigned integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadBits(int count)
    {
        if (count < 1 || count > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Bit count must be between 1 and 32.");
        }

        if (_bitPosition + count > _bitLength)
        {
            return 0;
        }

        if (_bufferedBits < count)
        {
            FillBuffer();
        }

        // MSB-aligned: top `count` bits hold the next value; constant right-shift, no mask needed for <=32 bits.
        uint value = (uint)(_buffer >> (64 - count));
        _buffer <<= count;
        _bufferedBits -= count;
        _bitPosition += count;

        return value;
    }

    /// <summary>
    /// Advances the bit position to the next byte boundary, consuming any partial-byte remainder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AlignToNextByte()
    {
        int remainder = _bitPosition & 7;
        if (remainder == 0)
        {
            return;
        }

        int bitsToSkip = 8 - remainder;
        _bitPosition += bitsToSkip;

        if (_bufferedBits >= bitsToSkip)
        {
            _buffer <<= bitsToSkip;
            _bufferedBits -= bitsToSkip;
        }
        else
        {
            _bufferedBits = 0;
            _buffer = 0;
            _bufferedByteIndex = _bitPosition >> 3;
        }
    }

    /// <summary>
    /// Advances the reader by the specified number of values (each of the given bit width), skipping them without reading.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count, int bitsPerValue)
    {
        int totalBits = count * bitsPerValue;
        _bitPosition += totalBits;
        _bufferedBits = 0;
        _buffer = 0;
        _bufferedByteIndex = _bitPosition >> 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FillBuffer()
    {
        int bitsRemaining = 64 - _bufferedBits;
        int bytesRemaining = _data.Length - _bufferedByteIndex;
        int bytesToRead = Math.Min(bytesRemaining, bitsRemaining >> 3);

        if (bytesToRead >= 8)
        {
            // Only reachable when _bufferedBits == 0, so we can assign directly.
            _buffer = BinaryPrimitives.ReadUInt64BigEndian(_data.Slice(_bufferedByteIndex, 8));
            _bufferedByteIndex += 8;
            _bufferedBits = 64;
        }
        else
        {
            int endIndex = _bufferedByteIndex + bytesToRead;
            for (int i = _bufferedByteIndex; i < endIndex; i++)
            {
                _buffer |= (ulong)_data[i] << (56 - _bufferedBits);
                _bufferedBits += 8;
            }
            _bufferedByteIndex = endIndex;
        }
    }
}
