using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Bit writer for packing unsigned sample values into a destination span (MSB-first within byte).
/// Buffers up to 64 bits internally and flushes 8 bytes at a time. Caller must call
/// <see cref="Flush"/> after the last write.
/// </summary>
internal ref struct UintBitWriter
{
    private readonly Span<byte> _destination;
    private ulong _buffer;
    private int _bufferedBits;
    private int _byteIndex;

    public UintBitWriter(in Span<byte> destination) => _destination = destination;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBits(int count, uint value)
    {
        _buffer |= (ulong)value << (64 - _bufferedBits - count);
        _bufferedBits += count;
        if (_bufferedBits == 64)
        {
            WriteBuffer();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write8Bits(byte value)
    {
        _buffer |= (ulong)value << (56 - _bufferedBits);
        _bufferedBits += 8;
        if (_bufferedBits == 64)
        {
            WriteBuffer();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write16Bits(ushort value)
    {
        _buffer |= (ulong)value << (48 - _bufferedBits);
        _bufferedBits += 16;
        if (_bufferedBits == 64)
        {
            WriteBuffer();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteBuffer()
    {
        BinaryPrimitives.WriteUInt64BigEndian(_destination.Slice(_byteIndex, 8), _buffer);
        _byteIndex += 8;
        _bufferedBits = 0;
        _buffer = 0;
    }

    /// <summary>
    /// Flushes any remaining buffered bits to the destination. Safe to call multiple times.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        if (_bufferedBits == 0)
        {
            return;
        }

        int bytesToWrite = (_bufferedBits + 7) >> 3;
        for (int i = 0; i < bytesToWrite; i++)
        {
            _destination[_byteIndex + i] = (byte)(_buffer >> 56);
            _buffer <<= 8;
        }

        _byteIndex += bytesToWrite;
        _bufferedBits = 0;
        _buffer = 0;
    }
}
