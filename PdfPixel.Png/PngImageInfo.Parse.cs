using PdfPixel.Png.Decoding;
using PdfPixel.Png.Parsing;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;

namespace PdfPixel.Png;

public sealed partial class PngImageInfo
{
    private const int ChunkOverhead = 12;
    private const int MaxChunkLength = int.MaxValue;

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
                throw new InvalidDataException($"PNG chunk {PngChunkParser.FormatType(type)} at offset {offset} runs past the end of the file.");
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

                throw new InvalidDataException($"PNG chunk {PngChunkParser.FormatType(type)} at offset {dataOffset - 8} fails its CRC check.");
            }

            if (info == null)
            {
                if (type != PngChunkType.ImageHeader)
                {
                    throw new InvalidDataException("PNG file does not start with an IHDR chunk.");
                }

                info = PngChunkParser.ReadImageHeader(chunkData);
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
                    info.Palette = PngChunkParser.ReadPalette(chunkData);
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
                        throw new InvalidDataException($"PNG file holds the unknown critical chunk {PngChunkParser.FormatType(type)}.");
                    }

                    PngChunkParser.ReadAncillaryChunk(type, chunkData, info);
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
}
