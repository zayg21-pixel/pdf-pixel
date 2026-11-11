using PdfPixel.Imaging.Encoding;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Png;

namespace PdfPixel.Gold.Utilities;

/// <summary>
/// Reads, writes and compares the PNG snapshots of rendered pages.
/// </summary>
internal static class ImageUtilities
{
    private const int BytesPerPixel = 4;
    private const int BitsPerComponent = 8;
    private const int ChannelTolerance = 3;

    // Straight (unpremultiplied) RGBA is the layout a PNG stores, so a page written to disk and read
    // back gives the exact same bytes, which is what a pixel-perfect comparison needs.
    private static readonly PdfImageChannel[] RgbaChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];

    /// <summary>
    /// Writes a rendered page as a PNG, replacing any file already there.
    /// </summary>
    public static void SavePng(PdfImageSamples samples, string path) => File.WriteAllBytes(path, PdfImageSamplesPngEncoder.Encode(samples));

    /// <summary>
    /// Reads an 8-bit RGBA PNG written by an earlier run.
    /// </summary>
    public static PdfImageSamples LoadPng(string path)
    {
        byte[] png = File.ReadAllBytes(path);
        PngImageInfo info = PngImageInfo.Parse(png);
        PngRowDecoder rows = new(info, png);

        if (rows.BitsPerComponent != BitsPerComponent || rows.ComponentCount != BytesPerPixel || rows.ColorType != PngColorType.TruecolorAlpha)
        {
            throw new InvalidDataException($"{path} is not an 8-bit RGBA PNG.");
        }

        PdfImageSamples samples = new(rows.Width, rows.Height, PdfImageSampleFormat.Integer, BitsPerComponent, RgbaChannels, isPremultiplied: false);

        for (int row = 0; row < rows.Height; row++)
        {
            if (!rows.TryReadRow(samples.GetRow(row)))
            {
                throw new InvalidDataException($"{path} ends before row {row}.");
            }
        }

        return samples;
    }

    /// <summary>
    /// Number of pixels that differ by more than the channel tolerance between two pages of the same size.
    /// </summary>
    public static int CountDifferentPixels(PdfImageSamples golden, PdfImageSamples rendered)
    {
        ReadOnlySpan<byte> goldenPixels = golden.Pixels;
        ReadOnlySpan<byte> renderedPixels = rendered.Pixels;

        if (goldenPixels.SequenceEqual(renderedPixels))
        {
            return 0;
        }

        int differentPixels = 0;

        for (int offset = 0; offset + BytesPerPixel <= goldenPixels.Length; offset += BytesPerPixel)
        {
            if (!PixelsMatch(goldenPixels, renderedPixels, offset))
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
    public static PdfImageSamples CreateDiffImage(PdfImageSamples golden, PdfImageSamples rendered)
    {
        ReadOnlySpan<byte> goldenPixels = golden.Pixels;
        ReadOnlySpan<byte> renderedPixels = rendered.Pixels;
        PdfImageSamples diff = new(golden.Width, golden.Height, PdfImageSampleFormat.Integer, BitsPerComponent, RgbaChannels, isPremultiplied: false);

        for (int row = 0; row < diff.Height; row++)
        {
            Span<byte> diffRow = diff.GetRow(row);
            int rowOffset = row * diff.RowBytes;

            for (int offset = 0; offset + BytesPerPixel <= diffRow.Length; offset += BytesPerPixel)
            {
                bool matches = PixelsMatch(goldenPixels, renderedPixels, rowOffset + offset);

                diffRow[offset] = 255;
                diffRow[offset + 1] = matches ? (byte)255 : (byte)0;
                diffRow[offset + 2] = 255;
                diffRow[offset + 3] = 255;
            }
        }

        return diff;
    }

    private static bool PixelsMatch(ReadOnlySpan<byte> goldenPixels, ReadOnlySpan<byte> renderedPixels, int offset)
    {
        for (int channel = offset; channel < offset + BytesPerPixel; channel++)
        {
            if (Math.Abs(goldenPixels[channel] - renderedPixels[channel]) > ChannelTolerance)
            {
                return false;
            }
        }

        return true;
    }
}
