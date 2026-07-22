using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Low-level writer over raw SFNT binary data: fixed-width big-endian integers. Shared by every
/// per-table writer so the byte-level format only needs to be implemented once.
/// </summary>
internal sealed class SfntWriter
{
    private readonly List<byte> _output = [];

    /// <summary>
    /// Gets the number of bytes written so far.
    /// </summary>
    public int Length => _output.Count;

    public void WriteByte(byte value) => _output.Add(value);

    public void WriteBytes(in ReadOnlySpan<byte> data)
    {
        foreach (byte value in data)
        {
            _output.Add(value);
        }
    }

    public void WriteUInt16(ushort value)
    {
        _output.Add((byte)((value >> 8) & 0xFF));
        _output.Add((byte)(value & 0xFF));
    }

    public void WriteInt16(short value) => WriteUInt16((ushort)value);

    public void WriteUInt32(uint value)
    {
        _output.Add((byte)((value >> 24) & 0xFF));
        _output.Add((byte)((value >> 16) & 0xFF));
        _output.Add((byte)((value >> 8) & 0xFF));
        _output.Add((byte)(value & 0xFF));
    }

    public void WriteInt32(int value) => WriteUInt32((uint)value);

    public void WriteInt64(long value)
    {
        for (int shiftIndex = 7; shiftIndex >= 0; shiftIndex--)
        {
            _output.Add((byte)((value >> (shiftIndex * 8)) & 0xFF));
        }
    }

    public byte[] ToArray() => _output.ToArray();
}
