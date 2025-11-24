using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using PdfReader.Imaging.Decoding;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using PdfReader.Streams;
using SkiaSharp;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PdfReader.Imaging.Skia;

/// <summary>
/// Decodes PNG images using SkiaSharp with an fast path for compatible FlateDecode + PNG predictor images.
/// </summary>
internal class PngSkiaDecoder
{
    /// <summary>
    /// Fast PNG path: wrap original FlateDecode zlib stream (header + deflate blocks + Adler32) into a PNG
    /// without recompression or recomputing Adler32. Returns null when incompatibilities are detected.
    /// </summary>
    public static PdfImageDecodingResult DecodeAsPng(PdfImage image)
    {
        if (image == null)
        {
            return null;
        }

        if (PdfImageRowProcessor.ShouldConvertColor(image) || (image.ColorSpaceConverter.Components != 1 && image.ColorSpaceConverter.Components != 3))
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
        SKColor[] palette = null;

        var converter = image.ColorSpaceConverter;
        bool canApplyColorSpace = !ColorFilterDecode.ShouldApplyDecode(image.DecodeArray, image.ColorSpaceConverter.Components) && image.MaskArray == null;
        bool colorConverted = false;

        byte colorType;

        if (canApplyColorSpace && converter is IndexedConverter indexed)
        {
            colorConverted = true;
            palette = indexed.BuildPalette(image.RenderingIntent);
            colorType = 3; // Palette
        }
        else if (converter.Components == 1)
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

        byte[] iccBytes = null;

        if (image.ColorSpaceConverter is IccBasedConverter iccBased)
        {
            iccBytes = iccBased.Profile?.Bytes;
            colorConverted = true;
        }

        // Build PNG: reuse original zlib stream bytes directly as IDAT payload.
        using var pngStream = new MemoryStream();
        WritePngSignature(pngStream);
        WriteIhdrChunk(pngStream, width, height, (byte)bpc, colorType);

        if (palette != null && palette.Length > 0)
        {
            WritePlteChunk(pngStream, palette);
        }

        if (iccBytes != null && iccBytes.Length > 0)
        {
            WriteIccpChunk(pngStream, iccBytes);
        }

        byte[] idatPayload = rawEncodedData;
        WriteChunk(pngStream, "IDAT", idatPayload, 0, idatPayload.Length);
        WriteChunk(pngStream, "IEND", Array.Empty<byte>(), 0, 0);

        pngStream.Flush();
        pngStream.Position = 0;

        return new PdfImageDecodingResult(SKImage.FromEncodedData(pngStream))
        {
            DecodeApplied = colorConverted,
            MaskRemoved = colorConverted,
            ColorConverted = colorConverted,
        };
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

    private static void WriteIccpChunk(Stream stream, byte[] iccProfile)
    {
        // Profile name: "ICC profile" (PDF and PNG spec), null-terminated
        const string ProfileName = "ICC profile";
        byte[] nameBytes = Encoding.ASCII.GetBytes(ProfileName);
        using (var chunkData = new MemoryStream())
        {
            chunkData.Write(nameBytes, 0, nameBytes.Length);
            chunkData.WriteByte(0); // Null terminator
            chunkData.WriteByte(0); // Compression method: 0 = deflate
            using (var deflate = new DeflateStream(chunkData, CompressionLevel.Optimal, true))
            {
                deflate.Write(iccProfile, 0, iccProfile.Length);
            }
            byte[] chunkBytes = chunkData.ToArray();
            WriteChunk(stream, "iCCP", chunkBytes, 0, chunkBytes.Length);
        }
    }

    private static void WritePlteChunk(Stream stream, SKColor[] palette)
    {
        // Each entry is 3 bytes: R, G, B
        int count = palette.Length;
        byte[] plte = new byte[count * 3];
        for (int i = 0; i < count; i++)
        {
            plte[i * 3 + 0] = palette[i].Red;
            plte[i * 3 + 1] = palette[i].Green;
            plte[i * 3 + 2] = palette[i].Blue;
        }
        WriteChunk(stream, "PLTE", plte, 0, plte.Length);
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
