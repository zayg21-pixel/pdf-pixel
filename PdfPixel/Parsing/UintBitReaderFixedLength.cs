using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Efficiently reads fixed-width bit values from a byte array.
/// Simplifies reading by always reading at exact boundaries.
/// </summary>
internal ref struct UintBitReaderFixedLength
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly int _bitCount;
    private readonly int _inverseBitCount;
    private int _bufferedBits;
    private ulong _buffer;
    private int _bufferedByteIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="UintBitReaderFixedLength"/> struct.
    /// </summary>
    /// <param name="data">The data to read from.</param>
    /// <param name="bitCount">The bit width of each value (must be a power of 2, 1-32).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if bitCount is not a power of 2 between 1 and 32.</exception>
    public UintBitReaderFixedLength(in ReadOnlySpan<byte> data, int bitCount)
        : this(data, bitCount, startBit: 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UintBitReaderFixedLength"/> struct.
    /// </summary>
    /// <param name="data">The data to read from.</param>
    /// <param name="bitCount">The bit width of each value (must be a power of 2, 1-32).</param>
    /// <param name="startBit">Bit offset of the first value to read.</param>
    public UintBitReaderFixedLength(in ReadOnlySpan<byte> data, int bitCount, int startBit)
    {
        _data = data;
        _bitCount = bitCount;
        _inverseBitCount = 64 - bitCount;
        _buffer = 0;
        _bufferedByteIndex = startBit >> 3;

        int misalignedBits = startBit & 7;

        if (misalignedBits != 0)
        {
            FillBuffer();
            _buffer <<= misalignedBits;
            _bufferedBits -= misalignedBits;
        }
    }

    /// <summary>
    /// Reads the next value of fixed bit width.
    /// </summary>
    /// <returns>The next value as an unsigned integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Read()
    {
        if (_bufferedBits < _bitCount)
        {
            FillBuffer();
        }

        var value = (uint)(_buffer >> _inverseBitCount);
        _buffer <<= _bitCount;
        _bufferedBits -= _bitCount;

        return value;
    }

    /// <summary>
    /// Fills the internal buffer with up to 64 bits from the data span, MSB-aligned.
    /// </summary>
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
