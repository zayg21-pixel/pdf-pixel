using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Bit writer for packing unsigned sample values into a destination span (MSB-first within byte).
/// </summary>
internal ref struct UintBitWriter
{
    private readonly Span<byte> _buffer;
    private int _byteIndex;
    private int _bitsAvailable;

    public UintBitWriter(in Span<byte> buffer)
    {
        _buffer = buffer;
        _byteIndex = 0;
        _bitsAvailable = 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBits(int count, uint value)
    {
        // One bounds check here; subsequent accesses via Unsafe.Add are unchecked.
        ref byte cur = ref _buffer[_byteIndex];
        int shift = _bitsAvailable - count;
        if (shift >= 0)
        {
            cur |= (byte)(value << shift);
            _bitsAvailable = shift;
            if (shift == 0)
            {
                _byteIndex++;
                _bitsAvailable = 8;
            }
        }
        else
        {
            // Value spans two bytes. Cast to byte naturally truncates upper bits for the lower portion.
            int lowerBits = -shift;
            cur |= (byte)(value >> lowerBits);
            _byteIndex++;
            Unsafe.Add(ref cur, 1) = (byte)(value << (8 - lowerBits));
            _bitsAvailable = 8 - lowerBits;
            if (_bitsAvailable == 0)
            {
                _byteIndex++;
                _bitsAvailable = 8;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write8Bits(byte value)
    {
        if (_bitsAvailable != 8)
        {
            throw new InvalidOperationException("Writer is not byte-aligned.");
        }

        _buffer[_byteIndex] = value;
        _byteIndex++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write16Bits(ushort value)
    {
        if (_bitsAvailable != 8)
        {
            throw new InvalidOperationException("Writer is not byte-aligned.");
        }

        ref byte cur = ref _buffer[_byteIndex];
        cur = (byte)(value >> 8);
        Unsafe.Add(ref cur, 1) = (byte)value;
        _byteIndex += 2;
    }
}
