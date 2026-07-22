using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Low-level reader over raw SFNT binary data: fixed-width big-endian integers. Shared by the
/// container directory reader and every per-table reader so the byte-level format only needs to be
/// implemented once.
/// </summary>
/// <remarks>
/// Once a read runs past the end of the data, the reader is faulted: every subsequent <c>Read*</c>
/// call returns null, without moving the position further. A caller reading several fields in
/// sequence therefore only needs to check <see cref="IsValid"/> once at the end, instead of after
/// every individual read.
/// </remarks>
internal ref struct SfntReader
{
    private readonly ReadOnlySpan<byte> _data;

    public SfntReader(in ReadOnlySpan<byte> data)
    {
        _data = data;
        Position = 0;
        IsValid = true;
    }

    /// <summary>
    /// Gets the current read position within the data.
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    /// Gets the total length of the data being read.
    /// </summary>
    public readonly int Length => _data.Length;

    /// <summary>
    /// Gets a value indicating whether every read so far has stayed within bounds.
    /// </summary>
    public bool IsValid { get; private set; }

    public bool Skip(int byteCount)
    {
        if (!IsValid || byteCount < 0 || Position + byteCount > _data.Length)
        {
            IsValid = false;
            return false;
        }

        Position += byteCount;
        return true;
    }

    /// <summary>
    /// Moves the read position to an absolute offset, for tables (e.g. "cmap") whose subtables
    /// reference each other by offset rather than being laid out purely sequentially.
    /// </summary>
    public bool Seek(int position)
    {
        if (!IsValid || position < 0 || position > _data.Length)
        {
            IsValid = false;
            return false;
        }

        Position = position;
        return true;
    }

    public byte? ReadByte()
    {
        if (!IsValid || Position + 1 > _data.Length)
        {
            IsValid = false;
            return null;
        }

        byte value = _data[Position];
        Position += 1;
        return value;
    }

    public byte ReadByteOrDefault(byte defaultValue = 0) => ReadByte() ?? defaultValue;

    /// <summary>
    /// Reads a fixed number of bytes. On failure, faults the reader (see <see cref="IsValid"/>) and
    /// returns an empty span, since a span itself cannot be nullable.
    /// </summary>
    public ReadOnlySpan<byte> ReadBytes(int byteCount)
    {
        if (!IsValid || byteCount < 0 || Position + byteCount > _data.Length)
        {
            IsValid = false;
            return ReadOnlySpan<byte>.Empty;
        }

        ReadOnlySpan<byte> result = _data.Slice(Position, byteCount);
        Position += byteCount;
        return result;
    }

    public ushort? ReadUInt16()
    {
        if (!IsValid || Position + 2 > _data.Length)
        {
            IsValid = false;
            return null;
        }

        var value = (ushort)((_data[Position] << 8) | _data[Position + 1]);
        Position += 2;
        return value;
    }

    public ushort ReadUInt16OrDefault(ushort defaultValue = 0) => ReadUInt16() ?? defaultValue;

    public short? ReadInt16() => (short?)ReadUInt16();

    public short ReadInt16OrDefault(short defaultValue = 0) => (short?)ReadUInt16() ?? defaultValue;

    public uint? ReadUInt32()
    {
        if (!IsValid || Position + 4 > _data.Length)
        {
            IsValid = false;
            return null;
        }

        uint value = (uint)_data[Position] << 24
            | (uint)_data[Position + 1] << 16
            | (uint)_data[Position + 2] << 8
            | _data[Position + 3];
        Position += 4;
        return value;
    }

    public uint ReadUInt32OrDefault(uint defaultValue = 0) => ReadUInt32() ?? defaultValue;

    public int? ReadInt32() => (int?)ReadUInt32();

    public int ReadInt32OrDefault(int defaultValue = 0) => (int?)ReadUInt32() ?? defaultValue;

    public long? ReadInt64()
    {
        if (!IsValid || Position + 8 > _data.Length)
        {
            IsValid = false;
            return null;
        }

        long value = 0;
        for (int byteIndex = 0; byteIndex < 8; byteIndex++)
        {
            value = (value << 8) | _data[Position + byteIndex];
        }

        Position += 8;
        return value;
    }

    public long ReadInt64OrDefault(long defaultValue = 0) => ReadInt64() ?? defaultValue;
}
