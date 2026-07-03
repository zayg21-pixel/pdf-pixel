using SkiaSharp;

namespace PdfPixel.Console.Demo;

/// <summary>
/// Writes an uncompressed 32-bit BMP file directly from a rendered image's pixel buffer.
/// SkiaSharp's built-in encoders only support PNG, JPEG, and WebP, all of which spend time
/// compressing; BMP has no compression step, which matters when exporting many pages quickly.
/// </summary>
internal static class BitmapWriter
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int PixelDataOffset = FileHeaderSize + InfoHeaderSize;

    public static void Write(SKImage image, string path)
    {
        SKImageInfo bitmapInfo = new(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using SKBitmap bitmap = new(bitmapInfo);
        image.ReadPixels(bitmapInfo, bitmap.GetPixels(), bitmapInfo.RowBytes, 0, 0);

        int pixelDataSize = bitmapInfo.RowBytes * bitmapInfo.Height;

        using FileStream output = File.Create(path);
        using BinaryWriter writer = new(output);

        // BITMAPFILEHEADER
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(PixelDataOffset + pixelDataSize);
        writer.Write(0); // reserved
        writer.Write(PixelDataOffset);

        // BITMAPINFOHEADER
        writer.Write(InfoHeaderSize);
        writer.Write(bitmapInfo.Width);
        writer.Write(-bitmapInfo.Height); // negative height: pixel rows are stored top-down, matching Skia's buffer
        writer.Write((short)1); // color planes
        writer.Write((short)32); // bits per pixel
        writer.Write(0); // BI_RGB, uncompressed
        writer.Write(pixelDataSize);
        writer.Write(0); // horizontal resolution, unspecified
        writer.Write(0); // vertical resolution, unspecified
        writer.Write(0); // colors in palette, none
        writer.Write(0); // important colors, all

        writer.Write(bitmap.GetPixelSpan());
    }
}
