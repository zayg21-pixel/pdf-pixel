using PdfPixel.Png.Model;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;

namespace PdfPixel.Png.Parsing;

/// <summary>
/// Reads the chunks of a PNG file into a <see cref="PngImageInfo"/>.
/// </summary>
public static partial class PngReader
{
    private const int ChunkOverhead = 12;
    private const int MaxChunkLength = int.MaxValue;

    /// <summary>
    /// Reads every chunk of a PNG file up to IEND, without decompressing the image data.
    /// </summary>
    /// <param name="data">Complete PNG file.</param>
    /// <returns>The image header and the data of every other chunk.</returns>
    /// <exception cref="InvalidDataException">The file is not a PNG file, or a critical chunk is missing, damaged or unknown.</exception>
    public static PngImageInfo Read(in ReadOnlySpan<byte> data)
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
}
