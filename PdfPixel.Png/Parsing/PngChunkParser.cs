using PdfPixel.Png.Decoding;
using PdfPixel.Png.Streams;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;

namespace PdfPixel.Png.Parsing;

/// <summary>
/// Parses PNG chunk data into <see cref="PngImageInfo"/>.
/// </summary>
internal static class PngChunkParser
{
    private const int ChunkOverhead = 12;
    private const int MaxChunkLength = int.MaxValue;
    private const int MaxKeywordLength = 79;

    /// <summary>
    /// Reads every chunk of a PNG file up to IEND, without decompressing the image data.
    /// </summary>
    /// <param name="data">Complete PNG file.</param>
    /// <returns>The image header and the data of every other chunk.</returns>
    /// <exception cref="InvalidDataException">The file is not a PNG file, or a critical chunk is missing, damaged or unknown.</exception>
    public static PngImageInfo Parse(in ReadOnlySpan<byte> data)
    {
        if (data.Length < PngLayout.Signature.Length || !data.Slice(0, PngLayout.Signature.Length).SequenceEqual(PngLayout.Signature))
        {
            throw new InvalidDataException("PNG file does not start with the PNG signature.");
        }

        PngImageInfo? info = null;
        List<PngDataSegment> imageData = [];
        var hasImageEnd = false;
        int offset = PngLayout.Signature.Length;

        while (offset < data.Length && !hasImageEnd)
        {
            if (data.Length - offset < ChunkOverhead)
            {
                throw new InvalidDataException($"PNG file ends inside the chunk header at offset {offset}.");
            }

            uint length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset));
            uint type = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 4));

            if (length > MaxChunkLength || length > (uint)(data.Length - offset - ChunkOverhead))
            {
                throw new InvalidDataException($"PNG chunk {FormatType(type)} at offset {offset} runs past the end of the file.");
            }

            int dataOffset = offset + 8;
            ReadOnlySpan<byte> chunkData = data.Slice(dataOffset, (int)length);
            uint storedCrc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(dataOffset + (int)length));
            bool isAncillary = (type & PngChunkType.AncillaryBit) != 0;

            offset = dataOffset + (int)length + 4;

            if (Crc32.HashToUInt32(data.Slice(dataOffset - 4, (int)length + 4)) != storedCrc)
            {
                if (isAncillary)
                {
                    continue;
                }

                throw new InvalidDataException($"PNG chunk {FormatType(type)} at offset {dataOffset - 8} fails its CRC check.");
            }

            if (info == null)
            {
                if (type != PngChunkType.ImageHeader)
                {
                    throw new InvalidDataException("PNG file does not start with an IHDR chunk.");
                }

                info = ReadImageHeader(chunkData);
                continue;
            }

            switch (type)
            {
                case PngChunkType.ImageHeader:
                {
                    throw new InvalidDataException("PNG file holds more than one IHDR chunk.");
                }
                case PngChunkType.Palette:
                {
                    info.Palette = ReadPalette(chunkData);
                    break;
                }
                case PngChunkType.ImageData:
                {
                    imageData.Add(new PngDataSegment(dataOffset, (int)length));
                    break;
                }
                case PngChunkType.ImageEnd:
                {
                    hasImageEnd = true;
                    break;
                }
                default:
                {
                    if (!isAncillary)
                    {
                        throw new InvalidDataException($"PNG file holds the unknown critical chunk {FormatType(type)}.");
                    }

                    ReadAncillaryChunk(type, chunkData, info);
                    break;
                }
            }
        }

        if (info == null)
        {
            throw new InvalidDataException("PNG file holds no IHDR chunk.");
        }

        if (!hasImageEnd)
        {
            throw new InvalidDataException("PNG file ends without an IEND chunk.");
        }

        if (imageData.Count == 0)
        {
            throw new InvalidDataException("PNG file holds no IDAT chunk.");
        }

        if (info.ColorType == PngColorType.IndexedColor && info.Palette == null)
        {
            throw new InvalidDataException("Indexed-color PNG image has no PLTE chunk.");
        }

        info.ImageData = imageData;

        return info;
    }

    private static PngImageInfo ReadImageHeader(in ReadOnlySpan<byte> data)
    {
        if (data.Length != 13)
        {
            throw new InvalidDataException($"PNG IHDR chunk holds {data.Length} bytes, expected 13.");
        }

        uint width = BinaryPrimitives.ReadUInt32BigEndian(data);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4));

        if (width > int.MaxValue || height > int.MaxValue)
        {
            throw new InvalidDataException($"PNG image size {width}x{height} exceeds the PNG maximum.");
        }

        PngImageInfo info = new()
        {
            Width = (int)width,
            Height = (int)height,
            BitDepth = data[8],
            ColorType = (PngColorType)data[9],
            InterlaceMethod = (PngInterlaceMethod)data[12]
        };

        PngLayout.ValidateHeader(info.Width, info.Height, info.BitDepth, info.ColorType);

        if (data[10] != 0)
        {
            throw new InvalidDataException($"PNG compression method {data[10]} is not defined.");
        }

        if (data[11] != 0)
        {
            throw new InvalidDataException($"PNG filter method {data[11]} is not defined.");
        }

        if (info.InterlaceMethod != PngInterlaceMethod.None && info.InterlaceMethod != PngInterlaceMethod.Adam7)
        {
            throw new InvalidDataException($"PNG interlace method {data[12]} is not defined.");
        }

        return info;
    }

    private static byte[] ReadPalette(in ReadOnlySpan<byte> data)
    {
        if (data.Length == 0 || data.Length % 3 != 0 || data.Length > 256 * 3)
        {
            throw new InvalidDataException($"PNG PLTE chunk holds {data.Length} bytes, not 1 to 256 entries of 3 bytes.");
        }

        return data.ToArray();
    }

    private static string FormatType(uint type)
    {
        char[] letters = [(char)(type >> 24), (char)((type >> 16) & 0xFF), (char)((type >> 8) & 0xFF), (char)(type & 0xFF)];

        return new string(letters);
    }

    /// <summary>
    /// Stores the data of a modeled ancillary chunk in <paramref name="info"/>; malformed and other chunks are left out.
    /// </summary>
    private static void ReadAncillaryChunk(uint type, in ReadOnlySpan<byte> data, PngImageInfo info)
    {
        switch (type)
        {
            case PngChunkType.Transparency:
            {
                ReadTransparency(data, info);
                break;
            }
            case PngChunkType.IccProfile:
            {
                ReadIccProfile(data, info);
                break;
            }
            case PngChunkType.SignificantBits:
            {
                int expectedLength = (info.ColorType == PngColorType.IndexedColor) ? 3 : PngLayout.GetChannelCount(info.ColorType);
                if (data.Length == expectedLength)
                {
                    info.SignificantBits = data.ToArray();
                }

                break;
            }
            case PngChunkType.Background:
            {
                ReadBackground(data, info);
                break;
            }
            case PngChunkType.PhysicalDimensions:
            {
                ReadPhysicalDimensions(data, info);
                break;
            }
            case PngChunkType.Exif:
            {
                info.Exif = data.ToArray();
                break;
            }
        }
    }

    private static void ReadTransparency(in ReadOnlySpan<byte> data, PngImageInfo info)
    {
        switch (info.ColorType)
        {
            case PngColorType.Grayscale:
            {
                if (data.Length == 2)
                {
                    info.TransparentGray = BinaryPrimitives.ReadUInt16BigEndian(data);
                }

                break;
            }
            case PngColorType.Truecolor:
            {
                if (data.Length == 6)
                {
                    info.TransparentColor = ReadRgbSample(data);
                }

                break;
            }
            case PngColorType.IndexedColor:
            {
                if (info.Palette != null && data.Length <= info.Palette.Length / 3)
                {
                    info.PaletteAlpha = data.ToArray();
                }

                break;
            }
        }
    }

    private static void ReadBackground(in ReadOnlySpan<byte> data, PngImageInfo info)
    {
        switch (info.ColorType)
        {
            case PngColorType.Grayscale:
            case PngColorType.GrayscaleAlpha:
            {
                if (data.Length == 2)
                {
                    info.BackgroundGray = BinaryPrimitives.ReadUInt16BigEndian(data);
                }

                break;
            }
            case PngColorType.Truecolor:
            case PngColorType.TruecolorAlpha:
            {
                if (data.Length == 6)
                {
                    info.BackgroundColor = ReadRgbSample(data);
                }

                break;
            }
            case PngColorType.IndexedColor:
            {
                if (data.Length == 1)
                {
                    info.BackgroundPaletteIndex = data[0];
                }

                break;
            }
        }
    }

    private static void ReadPhysicalDimensions(in ReadOnlySpan<byte> data, PngImageInfo info)
    {
        if (data.Length != 9 || data[8] > (byte)PngUnit.Meter)
        {
            return;
        }

        info.PhysicalDimensions = new PngPhysicalDimensions(
            (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(data), int.MaxValue),
            (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4)), int.MaxValue),
            (PngUnit)data[8]);
    }

    private static void ReadIccProfile(in ReadOnlySpan<byte> data, PngImageInfo info)
    {
        int nameEnd = FindKeywordEnd(data);
        if (nameEnd < 0 || data.Length < nameEnd + 2 || data[nameEnd + 1] != 0)
        {
            return;
        }

        byte[]? profile = TryDecompress(data.Slice(nameEnd + 2));
        if (profile == null)
        {
            return;
        }

        info.IccProfileName = ReadLatin1(data.Slice(0, nameEnd));
        info.IccProfile = profile;
    }

    private static PngRgbSample ReadRgbSample(in ReadOnlySpan<byte> data)
    {
        return new(
            BinaryPrimitives.ReadUInt16BigEndian(data),
            BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2)),
            BinaryPrimitives.ReadUInt16BigEndian(data.Slice(4)));
    }

    /// <summary>
    /// Returns the index of the null separator ending a keyword of 1 to 79 bytes, or -1 when there is none.
    /// </summary>
    private static int FindKeywordEnd(in ReadOnlySpan<byte> data)
    {
        int keywordEnd = data.IndexOf((byte)0);

        return (keywordEnd >= 1 && keywordEnd <= MaxKeywordLength) ? keywordEnd : -1;
    }

    private static byte[]? TryDecompress(in ReadOnlySpan<byte> data)
    {
        try
        {
            return PngZlibDecoder.Decompress(data);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static string ReadLatin1(in ReadOnlySpan<byte> data)
    {
        var characters = new char[data.Length];
        for (int index = 0; index < data.Length; index++)
        {
            characters[index] = (char)data[index];
        }

        return new string(characters);
    }
}
