using PdfReader.Imaging.Model;
using PdfReader.Streams;
using SkiaSharp;
using System;
using System.IO;

namespace PdfReader.Imaging.Skia;

internal class PngSkiaDecoder
{
    /// <summary>
    /// Fast PNG path: wrap original FlateDecode zlib stream (header + deflate blocks + Adler32) into a PNG
    /// without recompression or recomputing Adler32. Returns null when incompatibilities are detected.
    /// </summary>
    public static SKImage DecodeAsPng(PdfImage image)
    {
        if (image == null)
        {
            return null;
        }

        if (!SkiaImageHelpers.CanUseSkiaFastPath(image))
        {
            return null;
        }

        var filters = PdfStreamDecoder.GetFilters(image.SourceObject);

        if (filters.Count != 1 || filters[0] != PdfFilterType.FlateDecode)
        {
            // we can potentially extend this to decode non-flate filters in advance and return as non-compressed PNG,
            // but that is quite rare case
            return null;
        }

        int? predictor = image.DecodeParms?.Predictor;

        if (!predictor.HasValue || predictor.Value < 10 || predictor.Value > 15)
        {
            return null;
        }

        int width = image.Width;
        int height = image.Height;
        int bpc = image.BitsPerComponent;

        var converter = image.ColorSpaceConverter;

        byte colorType;
        if (converter.Components == 1)
        {
            colorType = 0; // Grayscale
        }
        else if (converter.Components == 3)
        {
            colorType = 2; // Truecolor RGB
        }
        else
        {
            // Not supported by fast path
            return null;
        }

        using var rawEncoded = image.SourceObject.GetRawStream();
        using var memoryStream = new MemoryStream();
        rawEncoded.CopyTo(memoryStream);
        byte[] rawEncodedData = memoryStream.ToArray();

        if (!IsValidPdgStream(rawEncodedData))
        {
            return null;
        }

        // Build PNG: reuse original zlib stream bytes directly as IDAT payload.
        using var pngStream = new MemoryStream();
        WritePngSignature(pngStream);
        WriteIhdrChunk(pngStream, width, height, (byte)bpc, colorType);
        byte[] idatPayload = rawEncodedData;
        WriteChunk(pngStream, "IDAT", idatPayload, 0, idatPayload.Length);
        WriteChunk(pngStream, "IEND", Array.Empty<byte>(), 0, 0);

        pngStream.Flush();
        pngStream.Position = 0;

        return SKImage.FromEncodedData(pngStream);
    }

    private static bool IsValidPdgStream(ReadOnlyMemory<byte> rawEncoded)
    {
        if (rawEncoded.IsEmpty || rawEncoded.Length < 6)
        {
            return false; // Need at least zlib header + Adler32.
        }

        var span = rawEncoded.Span;
        byte cmf = span[0];
        byte flg = span[1];

        // Validate zlib header (RFC1950): CMF bits 0..3 compression method (8=deflate), FCHECK so that (CMF*256 + FLG) % 31 == 0.
        bool methodOk = (cmf & 0x0F) == 8;
        bool fcheckOk = (cmf << 8 | flg) % 31 == 0;
        bool dictFlag = (flg & 0x20) != 0; // FDICT must be 0 (no preset dictionary) for our use.
        if (!methodOk || !fcheckOk || dictFlag)
        {
            return false;
        }

        return true;
    }

    #region PNG helpers (minimal)

    private static void WritePngSignature(Stream stream)
    {
        stream.WriteByte(0x89);
        stream.WriteByte((byte)'P');
        stream.WriteByte((byte)'N');
        stream.WriteByte((byte)'G');
        stream.WriteByte(0x0D);
        stream.WriteByte(0x0A);
        stream.WriteByte(0x1A);
        stream.WriteByte(0x0A);
    }

    private static void WriteIhdrChunk(Stream stream, int width, int height, byte bitDepth, byte colorType)
    {
        byte[] ihdr = new byte[13];
        WriteInt32BigEndian(ihdr, 0, width);
        WriteInt32BigEndian(ihdr, 4, height);
        ihdr[8] = bitDepth;
        ihdr[9] = colorType;
        ihdr[10] = 0; // Compression method (deflate)
        ihdr[11] = 0; // Filter method
        ihdr[12] = 0; // Interlace method (none)
        WriteChunk(stream, "IHDR", ihdr, 0, ihdr.Length);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data, int offset, int length)
    {
        if (type == null || type.Length != 4)
        {
            throw new ArgumentException("PNG chunk type must be 4 characters.", nameof(type));
        }
        WriteInt32BigEndian(stream, length);
        byte[] typeBytes = new byte[4]
        {
            (byte)type[0],
            (byte)type[1],
            (byte)type[2],
            (byte)type[3]
        };
        stream.Write(typeBytes, 0, 4);
        if (length > 0)
        {
            stream.Write(data, offset, length);
        }
        uint crc = UpdateCrc(0xFFFFFFFFu, typeBytes, 0, 4);
        if (length > 0)
        {
            crc = UpdateCrc(crc, data, offset, length);
        }
        crc ^= 0xFFFFFFFFu;
        WriteUInt32BigEndian(stream, crc);
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24 & 0xFF));
        stream.WriteByte((byte)(value >> 16 & 0xFF));
        stream.WriteByte((byte)(value >> 8 & 0xFF));
        stream.WriteByte((byte)(value & 0xFF));
    }

    private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24 & 0xFF);
        buffer[offset + 1] = (byte)(value >> 16 & 0xFF);
        buffer[offset + 2] = (byte)(value >> 8 & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24 & 0xFF));
        stream.WriteByte((byte)(value >> 16 & 0xFF));
        stream.WriteByte((byte)(value >> 8 & 0xFF));
        stream.WriteByte((byte)(value & 0xFF));
    }

    private static uint[] _crcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                if ((c & 1) != 0)
                {
                    c = 0xEDB88320u ^ c >> 1;
                }
                else
                {
                    c >>= 1;
                }
            }
            table[n] = c;
        }
        return table;
    }

    private static uint UpdateCrc(uint crc, byte[] data, int offset, int count)
    {
        uint c = crc;
        for (int i = 0; i < count; i++)
        {
            c = _crcTable[(c ^ data[offset + i]) & 0xFF] ^ c >> 8;
        }
        return c;
    }

    #endregion
}
