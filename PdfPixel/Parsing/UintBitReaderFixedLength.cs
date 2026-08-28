using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Efficiently reads fixed-width bit values from a byte array.
/// Simplifies reading by always reading at exact boundaries.
/// </summary>
internal ref struct UintBitReaderFixedLength
{
#if NET5_0_OR_GREATER
    private ref byte _dataReference;
#else
    private readonly ReadOnlySpan<byte> _data;
    private int _bufferedByteIndex;
#endif
    private readonly int _bitCount;
    private readonly int _inverseBitCount;
    private int _bytesRemaining;
    private int _bufferedBits;
    private ulong _buffer;

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
        int startByteIndex = Math.Min(startBit >> 3, data.Length);

#if NET5_0_OR_GREATER
        _dataReference = ref Unsafe.Add(ref MemoryMarshal.GetReference(data), startByteIndex);
#else
        _data = data;
        _bufferedByteIndex = startByteIndex;
#endif

        _bitCount = bitCount;
        _inverseBitCount = 64 - bitCount;
        _bytesRemaining = data.Length - startByteIndex;
        _buffer = 0;

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
        int bytesToRead = Math.Min(_bytesRemaining, bitsRemaining >> 3);

#if NET5_0_OR_GREATER
        ref byte dataReference = ref _dataReference;
#else
        ref byte dataReference = ref Unsafe.Add(ref MemoryMarshal.GetReference(_data), _bufferedByteIndex);
#endif

        if (bytesToRead >= 8)
        {
            ulong window = Unsafe.ReadUnaligned<ulong>(ref dataReference);
            _buffer = (BitConverter.IsLittleEndian) ? BinaryPrimitives.ReverseEndianness(window) : window;
            _bufferedBits = 64;
        }
        else
        {
            for (int byteOffset = 0; byteOffset < bytesToRead; byteOffset++)
            {
                _buffer |= (ulong)Unsafe.Add(ref dataReference, byteOffset) << (56 - _bufferedBits);
                _bufferedBits += 8;
            }
        }

        Advance(bytesToRead);
    }

    /// <summary>
    /// Moves the read position forward by the given number of bytes.
    /// </summary>
    /// <param name="byteCount">The number of bytes consumed from the data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Advance(int byteCount)
    {
        _bytesRemaining -= byteCount;

#if NET5_0_OR_GREATER
        _dataReference = ref Unsafe.Add(ref _dataReference, byteCount);
#else
        _bufferedByteIndex += byteCount;
#endif
    }
}
