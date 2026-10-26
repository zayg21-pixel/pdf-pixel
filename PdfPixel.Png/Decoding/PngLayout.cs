using System;
using System.IO;

namespace PdfPixel.Png.Decoding;

/// <summary>
/// Sample layout of the PNG color types (PNG Third Edition, table 11.1).
/// </summary>
internal static class PngLayout
{
    /// <summary>
    /// The eight bytes every PNG file starts with.
    /// </summary>
    public static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Returns the number of samples per pixel of a color type.
    /// </summary>
    public static int GetChannelCount(PngColorType colorType)
    {
        return colorType switch
        {
            PngColorType.Grayscale => 1,
            PngColorType.Truecolor => 3,
            PngColorType.IndexedColor => 1,
            PngColorType.GrayscaleAlpha => 2,
            PngColorType.TruecolorAlpha => 4,
            _ => throw new InvalidDataException($"PNG color type {(int)colorType} is not defined.")
        };
    }

    /// <summary>
    /// Throws when the dimensions, bit depth and color type do not form a valid image header.
    /// </summary>
    public static void ValidateHeader(int width, int height, int bitDepth, PngColorType colorType)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"PNG image size {width}x{height} is invalid.");
        }

        bool isValidDepth = colorType switch
        {
            PngColorType.Grayscale => bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8 || bitDepth == 16,
            PngColorType.IndexedColor => bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8,
            PngColorType.Truecolor or PngColorType.GrayscaleAlpha or PngColorType.TruecolorAlpha => bitDepth == 8 || bitDepth == 16,
            _ => throw new InvalidDataException($"PNG color type {(int)colorType} is not defined.")
        };

        if (!isValidDepth)
        {
            throw new InvalidDataException($"PNG color type {colorType} does not allow bit depth {bitDepth}.");
        }
    }

    /// <summary>
    /// Returns the number of bits of one pixel.
    /// </summary>
    public static int GetBitsPerPixel(int bitDepth, PngColorType colorType) => bitDepth * GetChannelCount(colorType);

    /// <summary>
    /// Returns the distance in bytes between a byte and the matching byte of the pixel to its left, at least 1
    /// (PNG Third Edition, section 7.3).
    /// </summary>
    public static int GetFilterBytesPerPixel(int bitsPerPixel) => Math.Max(1, bitsPerPixel / 8);

    /// <summary>
    /// Returns the byte length of a row of packed pixels.
    /// </summary>
    public static int GetRowBytes(int width, int bitsPerPixel) => (int)((((long)width * bitsPerPixel) + 7) / 8);
}
