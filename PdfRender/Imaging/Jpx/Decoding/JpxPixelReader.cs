using System;
using System.Runtime.CompilerServices;

namespace PdfRender.Imaging.Jpx.Decoding;

/// <summary>
/// High-performance pixel reader for JPX tile data.
/// Works directly with a span and uses compile-time known bit depth and signedness.
/// </summary>
internal ref struct JpxPixelReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly int _bitDepth;
    private readonly bool _isSigned;
    private int _position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JpxPixelReader(ReadOnlySpan<byte> data, int bitDepth, bool isSigned)
    {
        _data = data;
        _bitDepth = bitDepth;
        _isSigned = isSigned;
        _position = 0;
    }

    public bool CanRead
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position < _data.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadPixel()
    {
        return _bitDepth switch
        {
            <= 8 => ReadByte8(),
            <= 16 => ReadByte16(),
            _ => ReadByte32()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadByte8()
    {
        byte value = _data[_position++];
        return _isSigned ? (sbyte)value : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadByte16()
    {
        ushort value = (ushort)(_data[_position] << 8 | _data[_position + 1]);
        _position += 2;
        return _isSigned ? (short)value : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadByte32()
    {
        uint value = (uint)(_data[_position] << 24 | _data[_position + 1] << 16 | _data[_position + 2] << 8 | _data[_position + 3]);
        _position += 4;
        return _isSigned ? (int)value : (int)Math.Min(value, int.MaxValue);
    }
}