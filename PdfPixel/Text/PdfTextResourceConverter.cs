using PdfPixel.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.Text;

/// <summary>
/// Provides helpers to deserialize resource blobs used by PdfPixel.
/// </summary>
internal static class PdfTextResourceConverter
{
    /// <summary>
    /// Reads a binary blob and reconstructs the character map.
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
            index++;

            if (index + keyLength > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading key bytes.");
            }

            PdfString pdfString = new(blobMemory.Slice(index, keyLength));
            index += keyLength;

            if (index + 1 > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading value length.");
            }

            int valueLength = blob[index];
            index++;

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
    /// Reads a binary blob and reconstructs the CID-to-Unicode map.
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
            index++;

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
    /// Decodes a blob back into a glyph-name-to-width map.
    /// Format per entry: [UTF-8 name bytes][0x00 terminator][2 bytes width, ushort, little-endian]
    /// </summary>
    /// <param name="blob">The binary blob.</param>
    /// <returns>The decoded glyph-name-to-width map.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blob"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the blob is malformed.</exception>
    public static Dictionary<PdfString, ushort> FromWidthMapBlob(byte[] blob)
    {
        if (blob == null)
        {
            throw new ArgumentNullException(nameof(blob));
        }

        Dictionary<PdfString, ushort> result = [];
        ReadOnlyMemory<byte> blobMemory = blob.AsMemory();
        int index = 0;

        while (index < blob.Length)
        {
            int nameStart = index;
            while (index < blob.Length && blob[index] != 0)
            {
                index++;
            }

            if (index >= blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading glyph name.");
            }

            PdfString name = new(blobMemory.Slice(nameStart, index - nameStart));
            index++; // skip the null terminator

            if (index + 2 > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading width.");
            }

            ushort width = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(index, 2));
            index += 2;

            result[name] = width;
        }

        return result;
    }

    /// <summary>
    /// Decodes a blob back into an array of <see cref="PdfString"/>.
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

        var result = new PdfString[count];
        int offset = 4;
        ReadOnlyMemory<byte> blobMemory = blob.AsMemory();

        for (uint itemIndex = 0; itemIndex < count; itemIndex++)
        {
            if (offset + 1 > blob.Length)
            {
                throw new FormatException("Unexpected end of blob while reading string length.");
            }

            int length = blob[offset];
            offset++;

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
