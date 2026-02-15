using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Bit writer for packing unsigned sample values into a destination span (MSB-first within byte).
/// </summary>
internal ref struct UintBitWriter
{
    private Span<byte> _buffer;
    private int _bitPosition;

    public UintBitWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _bitPosition = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBits(int count, uint value)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            int bit = (int)((value >> i) & 1u);
            int byteIndex = _bitPosition >> 3;
            int bitIndex = 7 - (_bitPosition & 7);
            _buffer[byteIndex] = (byte)(_buffer[byteIndex] | (bit << bitIndex));
            _bitPosition++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write8Bits(byte value)
    {
        if ((_bitPosition & 7) != 0)
        {
            throw new InvalidOperationException("Writer is not byte-aligned.");
        }

        int byteIndex = _bitPosition >> 3;
        _buffer[byteIndex] = value;
        _bitPosition += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write16Bits(ushort value)
    {
        if ((_bitPosition & 7) != 0)
        {
            throw new InvalidOperationException("Writer is not byte-aligned.");
        }

        int byteIndex = _bitPosition >> 3;
        _buffer[byteIndex] = (byte)(value >> 8);
        _buffer[byteIndex + 1] = (byte)value;
        _bitPosition += 16;
    }
}
