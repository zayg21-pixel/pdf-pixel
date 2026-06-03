using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using PdfPixel.Models;

namespace PdfPixel.ResourceGenerator.Encodings;

/// <summary>
/// Serializes resource data into the binary blob formats read by PdfPixel at runtime.
/// </summary>
internal static class TextResourceSerializer
{
    /// <summary>
    /// Generates a binary blob representing the <paramref name="characterMap"/> contents.
    /// Format per entry: [1 byte keyLength][key bytes][1 byte valueLength][UTF-8 value bytes]
    /// </summary>
    public static byte[] GenerateCharacterMapBlob(Dictionary<PdfString, string> characterMap)
    {
        if (characterMap.Count == 0)
        {
            return Array.Empty<byte>();
        }

        int totalLength = 0;
        foreach (KeyValuePair<PdfString, string> entry in characterMap)
        {
            ReadOnlySpan<byte> keySpan = entry.Key.Value.Span;
            byte[] valueBytes = Encoding.UTF8.GetBytes(entry.Value);

            if (keySpan.Length > byte.MaxValue)
            {
                throw new InvalidOperationException("Key length exceeds single-byte limit (255).");
            }

            if (valueBytes.Length > byte.MaxValue)
            {
                throw new InvalidOperationException("Value length exceeds single-byte limit (255).");
            }

            totalLength += 1 + keySpan.Length + 1 + valueBytes.Length;
        }

        var blob = new byte[totalLength];
        int offset = 0;

        foreach (KeyValuePair<PdfString, string> entry in characterMap)
        {
            ReadOnlySpan<byte> keySpan = entry.Key.Value.Span;
            byte[] valueBytes = Encoding.UTF8.GetBytes(entry.Value);

            blob[offset++] = (byte)keySpan.Length;
            keySpan.CopyTo(blob.AsSpan(offset, keySpan.Length));
            offset += keySpan.Length;

            blob[offset++] = (byte)valueBytes.Length;
            valueBytes.AsSpan().CopyTo(blob.AsSpan(offset, valueBytes.Length));
            offset += valueBytes.Length;
        }

        return blob;
    }

    /// <summary>
    /// Generates a binary blob representing the CID-to-Unicode map.
    /// Format per entry: [4 bytes CID uint32 LE][1 byte valueLength][UTF-8 value bytes]
    /// </summary>
    public static byte[] GenerateCidToUnicodeMapBlob(Dictionary<uint, string> cidToUnicodeMap)
    {
        if (cidToUnicodeMap == null || cidToUnicodeMap.Count == 0)
        {
            return Array.Empty<byte>();
        }

        int totalLength = 0;
        foreach (KeyValuePair<uint, string> entry in cidToUnicodeMap)
        {
            byte[] valueBytes = Encoding.UTF8.GetBytes(entry.Value);
            if (valueBytes.Length > byte.MaxValue)
            {
                throw new InvalidOperationException($"Value length exceeds single-byte limit (255). CID: {entry.Key}");
            }

            totalLength += 4 + 1 + valueBytes.Length;
        }

        var blob = new byte[totalLength];
        int offset = 0;

        foreach (KeyValuePair<uint, string> entry in cidToUnicodeMap)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(offset, 4), entry.Key);
            offset += 4;

            byte[] valueBytes = Encoding.UTF8.GetBytes(entry.Value);
            blob[offset++] = (byte)valueBytes.Length;
            valueBytes.CopyTo(blob, offset);
            offset += valueBytes.Length;
        }

        return blob;
    }

    /// <summary>
    /// Generates a binary blob for an array of <see cref="PdfString"/> values.
    /// Format: [uint32 count][byte length + data per string]
    /// </summary>
    public static byte[] GeneratePdfStringBlob(PdfString[] strings)
    {
        if (strings == null || strings.Length == 0)
        {
            var emptyBlob = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(emptyBlob.AsSpan(0, 4), 0u);
            return emptyBlob;
        }

        int totalSize = 4;
        for (int i = 0; i < strings.Length; i++)
        {
            int length = strings[i].Value.Length;
            if (length > byte.MaxValue)
            {
                throw new InvalidOperationException("PdfString length exceeds 255 bytes.");
            }

            totalSize += 1 + length;
        }

        var blob = new byte[totalSize];
        BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(0, 4), (uint)strings.Length);

        int offset = 4;
        for (int i = 0; i < strings.Length; i++)
        {
            ReadOnlyMemory<byte> value = strings[i].Value;
            blob[offset++] = (byte)value.Length;
            if (value.Length > 0)
            {
                value.Span.CopyTo(blob.AsSpan(offset, value.Length));
                offset += value.Length;
            }
        }

        return blob;
    }
}
