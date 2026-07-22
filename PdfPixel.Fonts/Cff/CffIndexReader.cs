using System;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Reads CFF INDEX structures.
/// </summary>
internal static class CffIndexReader
{
    /// <summary>
    /// Reads a CFF INDEX structure and returns the entry count, data start position, and offsets.
    /// </summary>
    /// <param name="reader">CFF data reader positioned at the start of the INDEX.</param>
    /// <param name="count">Number of entries in the INDEX.</param>
    /// <param name="dataStart">Absolute position where the INDEX data begins.</param>
    /// <param name="offsets">Array of offsets (length = count + 1). Offsets are 1-based.</param>
    /// <param name="nextAfterIndex">Position immediately after the INDEX.</param>
    /// <returns>True if reading succeeded, false otherwise.</returns>
    public static bool TryReadIndex(ref CffDataReader reader, out int count, out int dataStart, out int[] offsets, out int nextAfterIndex)
    {
        count = 0;
        dataStart = 0;
        nextAfterIndex = 0;
        offsets = Array.Empty<int>();

        if (!reader.TryReadUInt16BE(out ushort entryCount))
        {
            return false;
        }

        count = entryCount;
        if (count == 0)
        {
            nextAfterIndex = reader.Position;
            return true;
        }

        if (!reader.TryReadByte(out byte offSize))
        {
            return false;
        }

        int offsetEntryCount = count + 1;
        offsets = new int[offsetEntryCount];
        for (int offsetIndex = 0; offsetIndex < offsetEntryCount; offsetIndex++)
        {
            if (!reader.TryReadOffset(offSize, out int entryOffset))
            {
                return false;
            }

            offsets[offsetIndex] = entryOffset;
        }

        dataStart = reader.Position;
        int dataSize = offsets[offsetEntryCount - 1] - 1;
        nextAfterIndex = dataStart + dataSize;
        reader.Position = nextAfterIndex;
        return true;
    }
}
