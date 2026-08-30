using System;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;

namespace PdfPixel.Examples;

/// <summary>
/// Writes a PNG file from the packed rows the decoders produce.
/// </summary>
internal static class PngWriter
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private const uint AdlerModulo = 65521;

    /// <summary>
    /// Writes <paramref name="rows"/> as a PNG image.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="bitDepth">Bits per sample: 1, 2, 4, 8, or 16.</param>
    /// <param name="colorType">Samples each pixel carries.</param>
    /// <param name="rowBytes">Byte length of one row of <paramref name="rows"/>.</param>
    /// <param name="rows">Row buffer of <c>rowBytes * height</c> bytes, top row first.</param>
    public static void Write(string path, int width, int height, byte bitDepth, PngColorType colorType, int rowBytes, ReadOnlySpan<byte> rows)
    {
        using FileStream output = File.Create(path);

        output.Write(Signature, 0, Signature.Length);

        var imageHeader = new byte[13];
        WriteInt32BigEndian(imageHeader, 0, width);
        WriteInt32BigEndian(imageHeader, 4, height);
        imageHeader[8] = bitDepth;
        imageHeader[9] = (byte)colorType;
        imageHeader[10] = 0; // compression method: deflate
        imageHeader[11] = 0; // filter method
        imageHeader[12] = 0; // interlace method: none

        WriteChunk(output, "IHDR", imageHeader);
        WriteChunk(output, "IDAT", Compress(rowBytes, height, rows));
        WriteChunk(output, "IEND", []);
    }

    /// <summary>
    /// Deflates the scanlines into a zlib stream, prefixing each row with its filter byte.
    /// </summary>
    private static byte[] Compress(int rowBytes, int height, ReadOnlySpan<byte> rows)
    {
        using MemoryStream buffer = new();

        // zlib header: deflate, 32K window, default compression level (RFC 1950).
        buffer.WriteByte(0x78);
        buffer.WriteByte(0x9C);

        // DeflateStream emits raw DEFLATE, so the zlib header above and the Adler32 below are ours.
        uint adlerLow = 1;
        uint adlerHigh = 0;
        byte[] filterByte = [0];

        using (DeflateStream deflate = new(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<byte> row = rows.Slice(y * rowBytes, rowBytes);

                // Filter type 0 (None) keeps every row byte as it stands.
                deflate.Write(filterByte, 0, 1);
                deflate.Write(row);

                UpdateAdler32(filterByte, ref adlerLow, ref adlerHigh);
                UpdateAdler32(row, ref adlerLow, ref adlerHigh);
            }
        }

        var adler = new byte[4];
        WriteInt32BigEndian(adler, 0, (int)((adlerHigh << 16) | adlerLow));
        buffer.Write(adler, 0, adler.Length);

        return buffer.ToArray();
    }

    private static void UpdateAdler32(ReadOnlySpan<byte> data, ref uint low, ref uint high)
    {
        for (int index = 0; index < data.Length; index++)
        {
            low = (low + data[index]) % AdlerModulo;
            high = (high + low) % AdlerModulo;
        }
    }

    /// <summary>
    /// Writes one chunk: length, type, data, and the CRC over type and data.
    /// </summary>
    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        var length = new byte[4];
        WriteInt32BigEndian(length, 0, data.Length);
        output.Write(length, 0, length.Length);

        byte[] typeBytes = [(byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3]];
        output.Write(typeBytes, 0, typeBytes.Length);
        output.Write(data);

        Crc32 crc = new();
        crc.Append(typeBytes);
        crc.Append(data);

        var checksum = new byte[4];
        WriteInt32BigEndian(checksum, 0, (int)crc.GetCurrentHashAsUInt32());
        output.Write(checksum, 0, checksum.Length);
    }

    private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }
}
