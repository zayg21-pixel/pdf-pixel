using System;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Low-level reader over raw CFF binary data: bytes, big-endian integers, and INDEX structures.
/// </summary>
internal ref struct CffBinaryReader
{
    private readonly ReadOnlySpan<byte> _data;

    public CffBinaryReader(in ReadOnlySpan<byte> data)
    {
        _data = data;
        Position = 0;
    }

    /// <summary>
    /// Gets the current read position within the data.
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the reader has reached the end of the data.
    /// </summary>
    public readonly bool IsAtEnd => Position >= _data.Length;

    public bool TryReadByte(out byte value)
    {
        if (Position >= _data.Length)
        {
            value = 0;
            return false;
        }

        value = _data[Position++];
        return true;
    }

    public bool TryReadUInt16BE(out ushort value)
    {
        if (Position + 1 >= _data.Length)
        {
            value = 0;
            return false;
        }

        value = (ushort)((_data[Position] << 8) | _data[Position + 1]);
        Position += 2;
        return true;
    }

    public bool TryReadOffset(int offSize, out int value)
    {
        value = 0;
        if (offSize < 1 || offSize > 4)
        {
            return false;
        }

        if (Position + offSize > _data.Length)
        {
            return false;
        }

        for (int octetIndex = 0; octetIndex < offSize; octetIndex++)
        {
            value = (value << 8) | _data[Position + octetIndex];
        }

        Position += offSize;
        return true;
    }

    /// <summary>
    /// Reads a CFF INDEX structure at the current position.
    /// </summary>
    /// <returns>The parsed index, or null if the data is malformed.</returns>
    public CffIndex? ReadIndex()
    {
        if (!TryReadUInt16BE(out ushort entryCount))
        {
            return null;
        }

        if (entryCount == 0)
        {
            return new CffIndex(0, Position, Array.Empty<int>(), Position);
        }

        if (!TryReadByte(out byte offSize))
        {
            return null;
        }

        int offsetEntryCount = entryCount + 1;
        var offsets = new int[offsetEntryCount];
        for (int offsetIndex = 0; offsetIndex < offsetEntryCount; offsetIndex++)
        {
            if (!TryReadOffset(offSize, out int entryOffset))
            {
                return null;
            }

            offsets[offsetIndex] = entryOffset;
        }

        int dataStart = Position;
        int dataSize = offsets[offsetEntryCount - 1] - 1;
        int endPosition = dataStart + dataSize;
        Position = endPosition;

        return new CffIndex(entryCount, dataStart, offsets, endPosition);
    }
}
