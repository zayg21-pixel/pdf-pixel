using SkiaSharp;
using System.Runtime.InteropServices;

namespace PdfPixel.Gold.Utilities;

/// <summary>
/// Reads, writes and compares the PNG snapshots of rendered pages.
/// </summary>
internal static class ImageUtilities
{
    private const int BytesPerPixel = 4;

    /// <summary>
    /// Writes a rendered page as a PNG, replacing any file already there.
    /// </summary>
    public static void SavePng(SKBitmap bitmap, string path)
    {
        using SKImage image = SKImage.FromPixelCopy(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes);
        using SKData pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream output = File.Create(path);
        pngData.SaveTo(output);
    }

    /// <summary>
    /// Reads a PNG written by an earlier run.
    /// </summary>
    public static SKBitmap LoadPng(string path)
    {
        using SKImage image = SKImage.FromEncodedData(path);

        if (image == null)
        {
            throw new InvalidDataException($"{path} could not be decoded.");
        }

        return ReadPixels(image);
    }

    /// <summary>
    /// Number of pixels that differ between two pages of the same size.
    /// </summary>
    public static int CountDifferentPixels(SKBitmap golden, SKBitmap rendered)
    {
        ReadOnlySpan<byte> goldenPixels = golden.GetPixelSpan();
        ReadOnlySpan<byte> renderedPixels = rendered.GetPixelSpan();

        if (goldenPixels.SequenceEqual(renderedPixels))
        {
            return 0;
        }

        int differentPixels = 0;

        for (int offset = 0; offset + BytesPerPixel <= goldenPixels.Length; offset += BytesPerPixel)
        {
            if (!goldenPixels.Slice(offset, BytesPerPixel).SequenceEqual(renderedPixels.Slice(offset, BytesPerPixel)))
            {
                differentPixels++;
            }
        }

        return differentPixels;
    }

    /// <summary>
    /// Builds a diff image the same size as <paramref name="golden"/>: white where it matches
    /// <paramref name="rendered"/>, solid magenta where the two differ.
    /// </summary>
    public static SKBitmap CreateDiffImage(SKBitmap golden, SKBitmap rendered)
    {
        ReadOnlySpan<byte> goldenPixels = golden.GetPixelSpan();
        ReadOnlySpan<byte> renderedPixels = rendered.GetPixelSpan();

        byte[] diffPixels = new byte[goldenPixels.Length];

        for (int offset = 0; offset + BytesPerPixel <= goldenPixels.Length; offset += BytesPerPixel)
        {
            bool matches = goldenPixels.Slice(offset, BytesPerPixel).SequenceEqual(renderedPixels.Slice(offset, BytesPerPixel));

            diffPixels[offset] = 255;
            diffPixels[offset + 1] = matches ? (byte)255 : (byte)0;
            diffPixels[offset + 2] = 255;
            diffPixels[offset + 3] = 255;
        }

        SKImageInfo imageInfo = new(golden.Width, golden.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap diff = new(imageInfo);
        Marshal.Copy(diffPixels, 0, diff.GetPixels(), diffPixels.Length);

        return diff;
    }

    // Straight (unpremultiplied) RGBA is the layout a PNG stores, so a page written to disk and read
    // back gives the exact same bytes, which is what a pixel-perfect comparison needs.
    private static SKBitmap ReadPixels(SKImage image)
    {
        SKImageInfo imageInfo = new(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap bitmap = new(imageInfo);

        if (!image.ReadPixels(imageInfo, bitmap.GetPixels(), imageInfo.RowBytes, 0, 0))
        {
            bitmap.Dispose();

            throw new InvalidOperationException("pixels could not be read back.");
        }

        return bitmap;
    }
}
