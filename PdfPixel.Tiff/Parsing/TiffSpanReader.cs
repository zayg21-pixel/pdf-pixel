using System;
using System.Buffers.Binary;
using System.IO;

namespace PdfPixel.Tiff.Parsing;

/// <summary>
/// Reads values at absolute offsets of a TIFF file in the file's byte order.
/// </summary>
internal readonly ref struct TiffSpanReader
{
    private readonly ReadOnlySpan<byte> _span;

    public TiffSpanReader(in ReadOnlySpan<byte> span, bool isLittleEndian)
    {
        _span = span;
        IsLittleEndian = isLittleEndian;
    }

    public bool IsLittleEndian { get; }

    public int Length => _span.Length;

    public byte ReadByte(long offset) => Slice(offset, 1)[0];

    public ushort ReadUInt16(long offset)
    {
        ReadOnlySpan<byte> bytes = Slice(offset, 2);
        return IsLittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    public uint ReadUInt32(long offset)
    {
        ReadOnlySpan<byte> bytes = Slice(offset, 4);
        return IsLittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    public ulong ReadUInt64(long offset)
    {
        ReadOnlySpan<byte> bytes = Slice(offset, 8);
        return IsLittleEndian
            ? BinaryPrimitives.ReadUInt64LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    public ReadOnlySpan<byte> Slice(long offset, long count)
    {
        if (offset < 0 || count < 0 || offset + count > _span.Length)
        {
            throw new InvalidDataException($"TIFF read of {count} byte(s) at offset {offset} is outside the {_span.Length}-byte file.");
        }

        return _span.Slice((int)offset, (int)count);
    }
}
