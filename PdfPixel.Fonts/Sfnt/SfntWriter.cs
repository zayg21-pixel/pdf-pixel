using System;
using System.Buffers.Binary;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Low-level writer over raw SFNT binary data: fixed-width big-endian integers. Shared by every
/// per-table writer so the byte-level format only needs to be implemented once.
/// </summary>
internal sealed class SfntWriter
{
    private const int DefaultCapacity = 256;

    private byte[] _buffer;
    private int _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntWriter"/> class, growing its buffer as it is
    /// written to.
    /// </summary>
    public SfntWriter()
        : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntWriter"/> class with room for
    /// <paramref name="capacity"/> bytes up front, so a caller that knows how much it writes never
    /// grows the buffer - and one that knows the length exactly gets that buffer back from
    /// <see cref="Detach"/> without a copy.
    /// </summary>
    /// <param name="capacity">The number of bytes to make room for.</param>
    public SfntWriter(int capacity) => _buffer = (capacity > 0) ? new byte[capacity] : Array.Empty<byte>();

    /// <summary>
    /// Gets the number of bytes written so far.
    /// </summary>
    public int Length => _length;

    public void WriteByte(byte value)
    {
        EnsureCapacity(_length + sizeof(byte));
        _buffer[_length] = value;
        _length += sizeof(byte);
    }

    public void WriteBytes(in ReadOnlySpan<byte> data)
    {
        EnsureCapacity(_length + data.Length);
        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
    }

    public void WriteUInt16(ushort value)
    {
        EnsureCapacity(_length + sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(_buffer.AsSpan(_length), value);
        _length += sizeof(ushort);
    }

    public void WriteInt16(short value) => WriteUInt16((ushort)value);

    public void WriteUInt32(uint value)
    {
        EnsureCapacity(_length + sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(_buffer.AsSpan(_length), value);
        _length += sizeof(uint);
    }

    public void WriteInt32(int value) => WriteUInt32((uint)value);

    public void WriteInt64(long value)
    {
        EnsureCapacity(_length + sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(_buffer.AsSpan(_length), value);
        _length += sizeof(long);
    }

    /// <summary>
    /// Gives the bytes written so far to the caller and leaves this writer empty. A buffer that is
    /// exactly full is handed over whole, so a caller that constructed the writer with the final
    /// length pays no copy for it.
    /// </summary>
    public byte[] Detach()
    {
        byte[] detached = (_length == _buffer.Length) ? _buffer : _buffer.AsSpan(0, _length).ToArray();

        _buffer = Array.Empty<byte>();
        _length = 0;

        return detached;
    }

    private void EnsureCapacity(int requiredLength)
    {
        if (requiredLength <= _buffer.Length)
        {
            return;
        }

        int newCapacity = (_buffer.Length == 0) ? DefaultCapacity : _buffer.Length * 2;
        if (newCapacity < requiredLength)
        {
            newCapacity = requiredLength;
        }

        Array.Resize(ref _buffer, newCapacity);
    }
}
