using PdfPixel.Png.Model;
using PdfPixel.Png.Streams;
using System;
using System.Buffers.Binary;
using System.IO;

namespace PdfPixel.Png.Parsing;

public static partial class PngReader
{
    private const int MaxKeywordLength = 79;

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
