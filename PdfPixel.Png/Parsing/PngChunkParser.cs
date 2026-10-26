using PdfPixel.Png.Decoding;
using System;
using System.Buffers.Binary;
using System.IO;

namespace PdfPixel.Png.Parsing;

/// <summary>
/// Parses PNG chunk data into <see cref="PngImageInfo"/>.
/// </summary>
internal static class PngChunkParser
{
    private const int MaxKeywordLength = 79;

    public static PngImageInfo ReadImageHeader(in ReadOnlySpan<byte> data)
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

    public static byte[] ReadPalette(in ReadOnlySpan<byte> data)
    {
        if (data.Length == 0 || data.Length % 3 != 0 || data.Length > 256 * 3)
        {
            throw new InvalidDataException($"PNG PLTE chunk holds {data.Length} bytes, not 1 to 256 entries of 3 bytes.");
        }

        return data.ToArray();
    }

    public static string FormatType(uint type)
    {
        char[] letters = [(char)(type >> 24), (char)((type >> 16) & 0xFF), (char)((type >> 8) & 0xFF), (char)(type & 0xFF)];

        return new string(letters);
    }

    /// <summary>
    /// Stores the data of a modeled ancillary chunk in <paramref name="info"/>; malformed and other chunks are left out.
    /// </summary>
    public static void ReadAncillaryChunk(uint type, in ReadOnlySpan<byte> data, PngImageInfo info)
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
