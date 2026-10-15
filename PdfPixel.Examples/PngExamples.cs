using System;
using System.IO;
using System.IO.Compression;
using PdfPixel.Png.Decoding;
using PdfPixel.Png.Encoding;
using PdfPixel.Png.Model;
using PdfPixel.Png.Parsing;

namespace PdfPixel.Examples;

/// <summary>
/// Decoding PNG images: at full size, an interlaced image at a reduced size, and expanding a palette
/// with its transparency to RGBA.
/// </summary>
internal static class PngExamples
{
    private const string Format = "Png";
    private const string SourceFile = "baboon.png";
    private const string InterlacedSourceFile = "baboon-adam7.png";
    private const string PaletteSourceFile = "baboon-palette.png";

    /// <summary>
    /// Runs every PNG example.
    /// </summary>
    public static void Run()
    {
        Decode(SourceFile, "at full size", "baboon.png", descaleFactor: 1);

        // The descale factor must be 1, 2, 4 or 8. An Adam7 image stores every 8th, 4th and 2nd pixel in
        // its first passes, so a reduced decode inflates only those passes and stops.
        Decode(InterlacedSourceFile, "at a quarter of its size", "baboon-adam7-descale-4.png", descaleFactor: 4);

        ExpandPalette();
    }

    /// <summary>
    /// Decodes a PNG and writes its rows back out as a new PNG.
    /// </summary>
    /// <param name="sourceFileName">Name of the PNG in the input folder.</param>
    /// <param name="description">Text naming this variant on the console.</param>
    /// <param name="outputFileName">Name of the PNG written to the output folder.</param>
    /// <param name="descaleFactor">Reduction (1, 2, 4 or 8) applied to the decoded size.</param>
    private static void Decode(string sourceFileName, string description, string outputFileName, int descaleFactor)
    {
        Console.WriteLine($"[Png] Decoding {sourceFileName} {description}...");

        // The decoder reads the image data straight out of the file in memory.
        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, sourceFileName));

        // Reads every chunk up to IEND: the image header, palette, transparency and ICC profile.
        // The image data is located here, but not decompressed.
        PngImageInfo info = PngReader.Read(fileBytes);

        // Inflates and unfilters the image data as rows are reached.
        PngRowDecoder decoder = new(info, fileBytes, descaleFactor);

        // Rows arrive as interleaved samples at the file's own bit depth, one byte per sample here.
        var samples = new byte[decoder.RowBytes * decoder.Height];

        for (int row = 0; row < decoder.Height && decoder.TryReadRow(samples.AsSpan(row * decoder.RowBytes, decoder.RowBytes)); row++)
        {
        }

        // The output keeps the source's color type and bit depth, at the decoded size and not interlaced.
        PngImageInfo outputInfo = new()
        {
            Width = decoder.Width,
            Height = decoder.Height,
            BitDepth = decoder.BitsPerComponent,
            ColorType = decoder.ColorType
        };

        // Adaptive filtering picks a PNG filter per row before deflating.
        string outputPath = ExamplePaths.Output(Format, outputFileName);
        using FileStream output = File.Create(outputPath);
        PngWriter.Write(output, outputInfo, samples, decoder.RowBytes, adaptiveFiltering: true, CompressionLevel.Optimal);

        Console.WriteLine($"[Png]   {decoder.Width}x{decoder.Height} -> {ExamplePaths.Relative(outputPath)}");
    }

    /// <summary>
    /// Decodes an indexed-color PNG and expands its indices to RGBA through the palette and its transparency.
    /// </summary>
    private static void ExpandPalette()
    {
        Console.WriteLine($"[Png] Expanding the palette of {PaletteSourceFile}...");

        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, PaletteSourceFile));
        PngImageInfo info = PngReader.Read(fileBytes);

        // PLTE holds red, green and blue bytes per entry; an indexed-color file always carries one.
        byte[] palette = info.Palette ?? throw new InvalidDataException($"{PaletteSourceFile} has no palette.");

        // tRNS gives the alpha of the leading palette entries; entries past its end are opaque.
        byte[] paletteAlpha = info.PaletteAlpha ?? [];

        // An indexed-color image decodes to one palette index per pixel, 8 bits each in this file.
        PngRowDecoder decoder = new(info, fileBytes);
        var indices = new byte[decoder.RowBytes];
        var rgba = new byte[decoder.Width * decoder.Height * 4];

        for (int row = 0; row < decoder.Height && decoder.TryReadRow(indices); row++)
        {
            for (int x = 0; x < decoder.Width; x++)
            {
                int index = indices[x];
                int target = ((row * decoder.Width) + x) * 4;

                rgba[target] = palette[index * 3];
                rgba[target + 1] = palette[(index * 3) + 1];
                rgba[target + 2] = palette[(index * 3) + 2];
                rgba[target + 3] = (index < paletteAlpha.Length) ? paletteAlpha[index] : byte.MaxValue;
            }
        }

        PngImageInfo outputInfo = new() { Width = decoder.Width, Height = decoder.Height, BitDepth = 8, ColorType = PngColorType.TruecolorAlpha };

        string outputPath = ExamplePaths.Output(Format, "baboon-palette-rgba.png");
        using FileStream output = File.Create(outputPath);
        PngWriter.Write(output, outputInfo, rgba, decoder.Width * 4, adaptiveFiltering: true, CompressionLevel.Optimal);

        Console.WriteLine($"[Png]   {palette.Length / 3} palette entries, {paletteAlpha.Length} with alpha, {decoder.Width}x{decoder.Height} -> {ExamplePaths.Relative(outputPath)}");
    }
}
