using PdfPixel.Png.Decoding;
using PdfPixel.Png.Encoding;
using PdfPixel.Png.Parsing;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;

namespace PdfPixel.Png;

/// <summary>
/// Writes PNG files from a <see cref="PngImageInfo"/> and rows of packed pixels.
/// </summary>
public static class PngWriter
{
    private const int MaxImageDataChunkLength = 1 << 16;
    private const int MaxKeywordLength = 79;

    /// <summary>
    /// Writes a PNG file holding <paramref name="pixels"/> and the chunks <paramref name="info"/> carries.
    /// </summary>
    /// <param name="destination">Stream the file is written to.</param>
    /// <param name="info">Image header and chunks to write; transparency and background are written in the form
    /// of the color type.</param>
    /// <param name="pixels">Rows of interleaved samples at <see cref="PngImageInfo.BitDepth"/> bits, packed MSB-first
    /// with 16-bit samples big-endian, top row first. Indexed-color images hold palette indices.</param>
    /// <param name="rowBytes">Byte distance between the starts of consecutive rows in <paramref name="pixels"/>.</param>
    /// <param name="adaptiveFiltering">True to choose the filter type of each row by the minimum sum of absolute
    /// differences, with filter type None on indexed-color images and bit depths below 8; false for filter type
    /// None on every row.</param>
    /// <param name="compressionLevel">Deflate compression level of the image data and the ICC profile.</param>
    public static void Write(
        Stream destination,
        PngImageInfo info,
        in ReadOnlySpan<byte> pixels,
        int rowBytes,
        bool adaptiveFiltering,
        CompressionLevel compressionLevel)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        PngLayout.ValidateHeader(info.Width, info.Height, info.BitDepth, info.ColorType);

        int bitsPerPixel = PngLayout.GetBitsPerPixel(info.BitDepth, info.ColorType);
        int imageRowBytes = PngLayout.GetRowBytes(info.Width, bitsPerPixel);

        if (rowBytes < imageRowBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBytes), $"Row stride {rowBytes} is shorter than the {imageRowBytes}-byte image row.");
        }

        if (pixels.Length < ((long)rowBytes * (info.Height - 1)) + imageRowBytes)
        {
            throw new ArgumentException($"Pixel buffer holds {pixels.Length} bytes, too few for {info.Height} rows.", nameof(pixels));
        }

        if (info.ColorType == PngColorType.IndexedColor && info.Palette == null)
        {
            throw new ArgumentException("Indexed-color image info has no palette.", nameof(info));
        }

        destination.Write(PngLayout.Signature, 0, PngLayout.Signature.Length);

        WriteChunk(destination, PngChunkType.ImageHeader, BuildImageHeader(info));

        if (info.IccProfile != null)
        {
            WriteChunk(destination, PngChunkType.IccProfile, BuildIccProfile(info.IccProfileName, info.IccProfile, compressionLevel));
        }

        if (info.SignificantBits != null)
        {
            WriteChunk(destination, PngChunkType.SignificantBits, info.SignificantBits);
        }

        if (info.Palette != null)
        {
            WriteChunk(destination, PngChunkType.Palette, info.Palette);
        }

        byte[]? transparency = BuildTransparency(info);
        if (transparency != null)
        {
            WriteChunk(destination, PngChunkType.Transparency, transparency);
        }

        byte[]? background = BuildBackground(info);
        if (background != null)
        {
            WriteChunk(destination, PngChunkType.Background, background);
        }

        if (info.PhysicalDimensions != null)
        {
            WriteChunk(destination, PngChunkType.PhysicalDimensions, BuildPhysicalDimensions(info.PhysicalDimensions.Value));
        }

        if (info.Exif != null)
        {
            WriteChunk(destination, PngChunkType.Exif, info.Exif);
        }

        bool isAdaptive = adaptiveFiltering
            && info.ColorType != PngColorType.IndexedColor
            && info.BitDepth >= 8;

        byte[] imageData = CompressImageData(info, pixels, rowBytes, bitsPerPixel, imageRowBytes, isAdaptive, compressionLevel);

        for (int offset = 0; offset < imageData.Length; offset += MaxImageDataChunkLength)
        {
            WriteChunk(destination, PngChunkType.ImageData, imageData, offset, Math.Min(MaxImageDataChunkLength, imageData.Length - offset));
        }

        WriteChunk(destination, PngChunkType.ImageEnd, []);
    }

    private static byte[] CompressImageData(
        PngImageInfo info,
        in ReadOnlySpan<byte> pixels,
        int rowBytes,
        int bitsPerPixel,
        int imageRowBytes,
        bool isAdaptive,
        CompressionLevel compressionLevel)
    {
        using PngZlibEncoder encoder = new(compressionLevel);
        PngRowFilter filter = new(isAdaptive, PngLayout.GetFilterBytesPerPixel(bitsPerPixel), imageRowBytes);
        var row = new byte[imageRowBytes];

        if (info.InterlaceMethod == PngInterlaceMethod.Adam7)
        {
            for (int pass = 0; pass < PngAdam7.PassCount; pass++)
            {
                int passWidth = PngAdam7.GetPassWidth(pass, info.Width);
                int passHeight = PngAdam7.GetPassHeight(pass, info.Height);

                if (passWidth == 0 || passHeight == 0)
                {
                    continue;
                }

                int passRowBytes = PngLayout.GetRowBytes(passWidth, bitsPerPixel);
                filter.Reset();

                for (int passRow = 0; passRow < passHeight; passRow++)
                {
                    int sourceRow = PngAdam7.StartY[pass] + (passRow * PngAdam7.StepY[pass]);
                    ReadOnlySpan<byte> source = pixels.Slice(sourceRow * rowBytes, imageRowBytes);

                    Array.Clear(row, 0, passRowBytes);
                    for (int pixel = 0; pixel < passWidth; pixel++)
                    {
                        int sourceColumn = PngAdam7.StartX[pass] + (pixel * PngAdam7.StepX[pass]);
                        PngPixelCopy.Copy(source, sourceColumn, row, pixel, bitsPerPixel);
                    }

                    encoder.Write(filter.Filter(row, passRowBytes), 0, passRowBytes + 1);
                }
            }
        }
        else
        {
            for (int sourceRow = 0; sourceRow < info.Height; sourceRow++)
            {
                pixels.Slice(sourceRow * rowBytes, imageRowBytes).CopyTo(row);
                encoder.Write(filter.Filter(row, imageRowBytes), 0, imageRowBytes + 1);
            }
        }

        return encoder.Finish();
    }

    private static byte[] BuildImageHeader(PngImageInfo info)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(data, (uint)info.Width);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)info.Height);
        data[8] = (byte)info.BitDepth;
        data[9] = (byte)info.ColorType;
        data[12] = (byte)info.InterlaceMethod;

        return data;
    }

    private static byte[] BuildIccProfile(string? name, byte[] profile, CompressionLevel compressionLevel)
    {
        if (name == null || name.Length == 0 || name.Length > MaxKeywordLength)
        {
            throw new ArgumentException("ICC profile name must be 1 to 79 characters.", nameof(name));
        }

        byte[] compressed = PngZlibEncoder.Compress(profile, compressionLevel);
        var data = new byte[name.Length + 2 + compressed.Length];

        for (int index = 0; index < name.Length; index++)
        {
            if (name[index] == '\0' || name[index] > 0xFF)
            {
                throw new ArgumentException("ICC profile name must hold Latin-1 characters other than null.", nameof(name));
            }

            data[index] = (byte)name[index];
        }

        Buffer.BlockCopy(compressed, 0, data, name.Length + 2, compressed.Length);

        return data;
    }

    private static byte[]? BuildTransparency(PngImageInfo info)
    {
        switch (info.ColorType)
        {
            case PngColorType.Grayscale:
            {
                return (info.TransparentGray != null) ? BuildGray(info.TransparentGray.Value) : null;
            }
            case PngColorType.Truecolor:
            {
                return (info.TransparentColor != null) ? BuildRgbSample(info.TransparentColor.Value) : null;
            }
            case PngColorType.IndexedColor:
            {
                return info.PaletteAlpha;
            }
            default:
            {
                return null;
            }
        }
    }

    private static byte[]? BuildBackground(PngImageInfo info)
    {
        switch (info.ColorType)
        {
            case PngColorType.Grayscale:
            case PngColorType.GrayscaleAlpha:
            {
                return (info.BackgroundGray != null) ? BuildGray(info.BackgroundGray.Value) : null;
            }
            case PngColorType.Truecolor:
            case PngColorType.TruecolorAlpha:
            {
                return (info.BackgroundColor != null) ? BuildRgbSample(info.BackgroundColor.Value) : null;
            }
            default:
            {
                return (info.BackgroundPaletteIndex != null) ? [info.BackgroundPaletteIndex.Value] : null;
            }
        }
    }

    private static byte[] BuildPhysicalDimensions(in PngPhysicalDimensions dimensions)
    {
        var data = new byte[9];
        BinaryPrimitives.WriteUInt32BigEndian(data, (uint)dimensions.PixelsPerUnitX);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)dimensions.PixelsPerUnitY);
        data[8] = (byte)dimensions.Unit;

        return data;
    }

    private static byte[] BuildGray(ushort gray)
    {
        var data = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(data, gray);

        return data;
    }

    private static byte[] BuildRgbSample(in PngRgbSample sample)
    {
        var data = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(data, sample.Red);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), sample.Green);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), sample.Blue);

        return data;
    }

    private static void WriteChunk(Stream destination, uint type, byte[] data) => WriteChunk(destination, type, data, 0, data.Length);

    /// <summary>
    /// Writes one chunk: length, type, data, and the CRC over type and data.
    /// </summary>
    private static void WriteChunk(Stream destination, uint type, byte[] data, int offset, int count)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)count);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), type);
        destination.Write(header, 0, header.Length);
        destination.Write(data, offset, count);

        Crc32 crc = new();
        crc.Append(header.AsSpan(4, 4));
        crc.Append(data.AsSpan(offset, count));

        var checksum = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc.GetCurrentHashAsUInt32());
        destination.Write(checksum, 0, checksum.Length);
    }
}
