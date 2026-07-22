using PdfPixel.Fonts.Model;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.Fonts.Resources;

/// <summary>
/// Provides helpers to deserialize resource blobs used by PdfPixel.Fonts.
/// </summary>
internal static class FontResourceConverter
{
    /// <summary>
    /// Reads a binary blob and reconstructs the character map.
    /// </summary>
    /// <param name="blob">The binary blob.</param>
    /// <param name="target">Target dictionary to store mappings.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blob"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the blob is malformed.</exception>
    public static void ReadFromCharacterMapBlob(byte[] blob, Dictionary<PdfFontString, string> target)
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

            PdfFontString fontString = new(blobMemory.Slice(index, keyLength));
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

            string value = Encoding.UTF8.GetString(blob, index, valueLength);
            index += valueLength;

            target[fontString] = value;
        }

        if (index != blob.Length)
        {
            throw new FormatException("Blob parsing ended at unexpected position.");
        }
    }

    /// <summary>
    /// Decodes a blob back into an array of <see cref="PdfFontString"/>.
    /// </summary>
    /// <param name="blob">Binary blob.</param>
    /// <returns>Array of decoded <see cref="PdfFontString"/> (never null).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blob"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when blob is malformed.</exception>
    public static PdfFontString[] FromPdfStringBlob(byte[] blob)
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
            return Array.Empty<PdfFontString>();
        }

        var result = new PdfFontString[count];
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

            ReadOnlyMemory<byte> slice = blobMemory.Slice(offset, length);
            result[itemIndex] = new PdfFontString(slice);
            offset += length;
        }

        if (offset != blob.Length)
        {
            throw new FormatException("Extra unread bytes at end of blob (malformed).");
        }

        return result;
    }
}
