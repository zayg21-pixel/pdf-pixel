using PdfReader.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace PdfReader.Text;

/// <summary>
/// Provides helpers to serialize and deserialize arrays of <see cref="PdfString"/> to a compact binary blob.
/// </summary>
public static class PdfTextResourceConverter
{
    /// <summary>
    /// Generates a binary blob representing the <see cref="CharacterMap"/> contents.
    /// </summary>
    /// <remarks>
    /// Format per entry (sequentially appended):
    /// [1 byte keyLength][keyLength bytes key][1 byte valueLength][valueLength bytes UTF-8 value]
    /// Length fields must be &lt;=255. Throws <see cref="InvalidOperationException"/> if exceeded.
    /// </remarks>
    /// <returns>Binary blob of the character map.</returns>
    public static byte[] GenerateCharacterMapBlob(Dictionary<PdfString, string> characterMap)
    {
        if (characterMap.Count == 0)
        {
            return Array.Empty<byte>();
        }

        // First pass: compute required size and validate lengths.
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

        byte[] blob = new byte[totalLength];
        int offset = 0;

        foreach (KeyValuePair<PdfString, string> entry in characterMap)
        {
            ReadOnlySpan<byte> keySpan = entry.Key.Value.Span;
            byte[] valueBytes = Encoding.UTF8.GetBytes(entry.Value);

            // Key length
            blob[offset] = (byte)keySpan.Length;
            offset += 1;

            // Key bytes
            keySpan.CopyTo(blob.AsSpan(offset, keySpan.Length));
            offset += keySpan.Length;

            // Value length
            blob[offset] = (byte)valueBytes.Length;
            offset += 1;

            // Value bytes
            valueBytes.AsSpan().CopyTo(blob.AsSpan(offset, valueBytes.Length));
            offset += valueBytes.Length;
        }

        return blob;
    }

    /// <summary>
    /// Reads a binary blob produced by <see cref="GenerateCharacterMapBlob"/> and reconstructs the character map.
    /// Uses only <see cref="ReadOnlyMemory{T}"/> slices for key/value extraction.
    /// </summary>
    /// <param name="blob">The binary blob.</param>
    /// <param name="target">Target dictionary to store mappings.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blob"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the blob is malformed.</exception>
    public static void ReadFromCharacterMapBlob(byte[] blob, Dictionary<PdfString, string> target)
    {
        if (blob == null)
        {
            throw new ArgumentNullException(nameof(blob));
        }

        ReadOnlyMemory<byte> blobMemory = blob.AsMemory();

        int index = 0;

        while (index < blob.Length)
        {
            if (index + 1 > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading key length.");
            }

            int keyLength = blob[index];
            index += 1;

            if (index + keyLength > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading key bytes.");
            }

            var pdfString = new PdfString(blobMemory.Slice(index, keyLength));
            index += keyLength;

            if (index + 1 > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading value length.");
            }

            int valueLength = blob[index];
            index += 1;

            if (index + valueLength > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading value bytes.");
            }

            string value = Encoding.UTF8.GetString(blobMemory.Slice(index, valueLength));
            index += valueLength;

            target[pdfString] = value;
        }

        if (index != blob.Length)
        {
            throw new FormatException("Blob parsing ended at unexpected position.");
        }
    }

    /// <summary>
    /// Generates a binary blob representing the CID-to-Unicode map.
    /// </summary>
    /// <remarks>
    /// Format per entry (sequentially appended):
    /// [4 bytes CID (uint32, little-endian)][1 byte valueLength][valueLength bytes UTF-8 value]
    /// Length fields must be <=255. Throws <see cref="InvalidOperationException"/> if exceeded.
    /// </remarks>
    /// <param name="cidToUnicodeMap">The CID-to-Unicode mapping dictionary.</param>
    /// <returns>Binary blob of the CID-to-Unicode map.</returns>
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
                throw new InvalidOperationException("Value length exceeds single-byte limit (255). CID: " + entry.Key);
            }
            totalLength += 4 + 1 + valueBytes.Length;
        }

        byte[] blob = new byte[totalLength];
        int offset = 0;
        foreach (KeyValuePair<uint, string> entry in cidToUnicodeMap)
        {
            // Write CID (4 bytes, little-endian)
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(offset, 4), entry.Key);
            offset += 4;

            // Write value length and value bytes
            byte[] valueBytes = Encoding.UTF8.GetBytes(entry.Value);
            blob[offset] = (byte)valueBytes.Length;
            offset += 1;
            valueBytes.CopyTo(blob, offset);
            offset += valueBytes.Length;
        }
        return blob;
    }

    /// <summary>
    /// Reads a binary blob produced by <see cref="GenerateCidToUnicodeMapBlob"/> and reconstructs the CID-to-Unicode map.
    /// </summary>
    /// <param name="blob">The binary blob.</param>
    /// <param name="target">Target dictionary to store mappings.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blob"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the blob is malformed.</exception>
    public static void ReadFromCidToUnicodeMapBlob(byte[] blob, Dictionary<uint, string> target)
    {
        if (blob == null)
        {
            throw new ArgumentNullException(nameof(blob));
        }
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        int index = 0;
        while (index < blob.Length)
        {
            if (index + 4 > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading CID.");
            }
            uint cid = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(index, 4));
            index += 4;

            if (index + 1 > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading value length.");
            }
            int valueLength = blob[index];
            index += 1;

            if (index + valueLength > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading value bytes.");
            }
            string value = Encoding.UTF8.GetString(blob, index, valueLength);
            index += valueLength;

            target[cid] = value;
        }
        if (index != blob.Length)
        {
            throw new FormatException("Blob parsing ended at unexpected position.");
        }
    }

    /// <summary>
    /// Generates a binary blob for an array of <see cref="PdfString"/> values.
    /// </summary>
    /// <remarks>
    /// Format:
    /// [uint32 count]
    /// For each string (in order):
    /// [byte length][length bytes data]
    /// Length is limited to255 (single byte). Throws <see cref="InvalidOperationException"/> if exceeded.
    /// </remarks>
    /// <param name="strings">Source array (may be null or empty).</param>
    /// <returns>Binary blob representing the array.</returns>
    public static byte[] GeneratePdfStringBlob(PdfString[] strings)
    {
        if (strings == null || strings.Length == 0)
        {
            // Represent empty set:4 bytes count =0.
            byte[] emptyBlob = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(emptyBlob.AsSpan(0, 4), 0u);
            return emptyBlob;
        }

        int count = strings.Length;
        int totalSize = 4; // uint32 count header.

        for (int index = 0; index < count; index++)
        {
            ReadOnlyMemory<byte> value = strings[index].Value;
            int length = value.Length;
            if (length > byte.MaxValue)
            {
                throw new InvalidOperationException("PdfString length exceeds255 bytes and cannot be encoded in single-byte length field.");
            }
            totalSize += 1 + length;
        }

        byte[] blob = new byte[totalSize];
        // Write count (uint32 little-endian) directly.
        BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(0, 4), (uint)count);

        int offset = 4;
        for (int index = 0; index < count; index++)
        {
            ReadOnlyMemory<byte> value = strings[index].Value;
            int length = value.Length;
            blob[offset] = (byte)length;
            offset += 1;
            if (length > 0)
            {
                value.Span.CopyTo(blob.AsSpan(offset, length));
                offset += length;
            }
        }

        return blob;
    }

    /// <summary>
    /// Decodes a blob produced by <see cref="GeneratePdfStringBlob"/> back into an array of <see cref="PdfString"/>.
    /// </summary>
    /// <param name="blob">Binary blob.</param>
    /// <returns>Array of decoded <see cref="PdfString"/> (never null).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blob"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when blob is malformed.</exception>
    public static PdfString[] FromPdfStringBlob(byte[] blob)
    {
        if (blob == null)
        {
            throw new ArgumentNullException(nameof(blob));
        }

        if (blob.Length < 4)
        {
            throw new FormatException("Blob too short to contain count header.");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(0, 4));
        if (count == 0)
        {
            return Array.Empty<PdfString>();
        }

        PdfString[] result = new PdfString[count];
        int offset = 4;
        ReadOnlyMemory<byte> blobMemory = blob.AsMemory();

        for (uint itemIndex = 0; itemIndex < count; itemIndex++)
        {
            if (offset + 1 > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading string length.");
            }
            int length = blob[offset];
            offset += 1;

            if (offset + length > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading string data.");
            }

            // Slice from the single ReadOnlyMemory instance.
            ReadOnlyMemory<byte> slice = blobMemory.Slice(offset, length);
            result[itemIndex] = new PdfString(slice);
            offset += length;
        }

        if (offset != blob.Length)
        {
            throw new FormatException("Extra unread bytes at end of blob (malformed).");
        }

        return result;
    }
}
