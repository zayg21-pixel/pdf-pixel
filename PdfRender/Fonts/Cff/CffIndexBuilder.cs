using System.Collections.Generic;
using System.IO;

namespace PdfRender.Fonts.Cff;

/// <summary>
/// Provides methods for building CFF (Compact Font Format) index structures and header data.
/// </summary>
/// <remarks>
/// CFF indices are used to store font objects such as charstrings, subroutines, and strings in a compact, offset-based format.
/// This class offers helpers for constructing header, index, and offset data according to the CFF specification.
/// </remarks>
internal static class CffIndexBuilder
{
    /// <summary>
    /// Builds the CFF header bytes.
    /// </summary>
    /// <returns>The byte array representing the CFF header.</returns>
    public static byte[] BuildHeader()
    {
        // major=1, minor=0, headerSize=4, offSize=1 (minimal).
        return [1, 0, 4, 1];
    }

    /// <summary>
    /// Builds an empty CFF index.
    /// </summary>
    /// <returns>The byte array representing an empty CFF index.</returns>
    public static byte[] BuildEmptyIndex()
    {
        return [0, 0];
    }

    /// <summary>
    /// Builds a CFF index for a single object.
    /// </summary>
    /// <param name="data">The byte array of the object data.</param>
    /// <returns>The byte array representing the CFF index for the object.</returns>
    public static byte[] BuildSingleObjectIndex(byte[] data)
    {
        int endOffset = data.Length + 1; // First offset is always1.
        byte offSize = endOffset <= 0xFF ? (byte)1 : endOffset <= 0xFFFF ? (byte)2 : endOffset <= 0xFFFFFF ? (byte)3 : (byte)4;

        using var ms = new MemoryStream();
        ms.WriteByte(0); // count high byte (0 for1 object).
        ms.WriteByte(1); // count low byte.
        ms.WriteByte(offSize);
        WriteOffset(ms, 1, offSize); // First object starts at1.
        WriteOffset(ms, endOffset, offSize); // End of object.
        ms.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Writes an offset value to the stream.
    /// </summary>
    /// <param name="s">The stream to write the offset to.</param>
    /// <param name="value">The offset value.</param>
    /// <param name="size">The size in bytes of the offset.</param>
    public static void WriteOffset(Stream s, int value, int size)
    {
        for (int i = size - 1; i >= 0; i--)
        {
            int shift = i * 8;
            byte b = (byte)((value >> shift) & 0xFF);
            s.WriteByte(b);
        }
    }

    /// <summary>
    /// Builds a CFF index from a list of object data arrays.
    /// </summary>
    /// <param name="objects">The list of object data arrays.</param>
    /// <returns>The byte array representing the CFF index for the objects.</returns>
    public static byte[] BuildIndex(List<byte[]> objects)
    {
        if (objects == null || objects.Count == 0)
        {
            return BuildEmptyIndex();
        }

        int count = objects.Count;
        int currentOffset = 1; // First object offset is1 per CFF spec.
        List<int> offsets = new List<int>(count + 1) { 1 };
        foreach (byte[] obj in objects)
        {
            currentOffset += obj.Length;
            offsets.Add(currentOffset);
        }
        int maxOffset = offsets[offsets.Count - 1];
        byte offSize = maxOffset <= 0xFF ? (byte)1 : maxOffset <= 0xFFFF ? (byte)2 : maxOffset <= 0xFFFFFF ? (byte)3 : (byte)4;

        using var ms = new MemoryStream();
        ms.WriteByte((byte)(count >> 8));
        ms.WriteByte((byte)(count & 0xFF));
        ms.WriteByte(offSize);
        foreach (int offset in offsets)
        {
            WriteOffset(ms, offset, offSize);
        }
        foreach (byte[] obj in objects)
        {
            ms.Write(obj, 0, obj.Length);
        }
        return ms.ToArray();
    }
}
