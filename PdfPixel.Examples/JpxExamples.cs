using System;
using System.IO;
using System.IO.Compression;
using PdfPixel.Jpx;
using PdfPixel.Png;

namespace PdfPixel.Examples;

/// <summary>
/// Decoding JPEG 2000 codestreams: at full resolution, at a reduced resolution, and one tile on
/// its own.
/// </summary>
internal static class JpxExamples
{
    private const string Format = "Jpx";
    private const string SourceFile = "baboon.j2k";
    private const string TiledSourceFile = "baboon-tiles-256.j2k";
    private const string AlphaSourceFile = "baboon-alpha.jp2";

    /// <summary>
    /// Runs every JPEG 2000 example.
    /// </summary>
    public static void Run()
    {
        Decode(SourceFile, "at full resolution", "baboon.png", descaleFactor: 1);

        // The descale factor must be a power of two. Above 1 the inverse wavelet transform skips
        // its finest levels, which is faster than decoding in full and scaling down afterwards.
        Decode(SourceFile, "at a quarter of its resolution", "baboon-descale-4.png", descaleFactor: 4);

        // A tile is decoded on its own, so reading only the tiles a view needs pays off on a tiled
        // codestream. This file carries a grid of 256x256 tiles.
        DecodeTile(TiledSourceFile, "top-left tile", "baboon-tile.png", tileIndex: 0);

        // A raw codestream carries no boxes, so opacity needs the JP2 wrapper: its cdef box marks
        // one component as alpha. A ComponentSelection in JpxDecodingParameters that leaves that
        // component out keeps it from ever reaching the entropy decoder.
        Decode(AlphaSourceFile, "with its alpha channel", "baboon-alpha.png", descaleFactor: 1);
    }

    /// <summary>
    /// Decodes a codestream to a PNG.
    /// </summary>
    /// <param name="sourceFileName">Name of the codestream in the input folder.</param>
    /// <param name="description">Text naming this variant on the console.</param>
    /// <param name="outputFileName">Name of the PNG written to the output folder.</param>
    /// <param name="descaleFactor">Power-of-two reduction applied to the decoded resolution.</param>
    private static void Decode(string sourceFileName, string description, string outputFileName, int descaleFactor)
    {
        Console.WriteLine($"[Jpx] Decoding {sourceFileName} {description}...");

        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, sourceFileName));

        // Parses the main header: image and tile grid, coding style, quantization, and components.
        JpxHeader header = JpxHeader.Parse(fileBytes);

        JpxDecodingParameters decodingParameters = new(descaleFactor);

        // The tile reader decodes tiles on demand from the codestream that follows the header.
        JpxTileReader tileReader = new(header, fileBytes.AsMemory(header.CodestreamOffset), decodingParameters);

        // The converter assembles the tile grid into image rows, decoding each tile row as it is reached.
        JpxTileToRowConverter converter = new(header, tileReader);

        // Rows arrive bit-packed at the codestream's own precision, one byte per component here.
        int rowBytes = ((converter.Width * converter.ComponentCount * converter.BitsPerComponent) + 7) / 8;
        var rowBuffer = new byte[rowBytes];
        var samples = new byte[rowBytes * converter.Height];

        for (int row = 0; row < converter.Height && converter.TryGetNextRow(rowBuffer); row++)
        {
            rowBuffer.CopyTo(samples.AsSpan(row * rowBytes));
        }

        // A cdef box can declare an opacity component, which the converter appends to the colors.
        PngColorType colorType = (converter.ComponentCount == 4) ? PngColorType.TruecolorAlpha : PngColorType.Truecolor;
        PngImageInfo pngInfo = new() { Width = converter.Width, Height = converter.Height, BitDepth = 8, ColorType = colorType };
        string outputPath = ExamplePaths.Output(Format, outputFileName);
        using FileStream output = File.Create(outputPath);
        PngWriter.Write(output, pngInfo, samples, rowBytes, adaptiveFiltering: true, CompressionLevel.Optimal);

        Console.WriteLine($"[Jpx]   {converter.Width}x{converter.Height} -> {ExamplePaths.Relative(outputPath)}");
    }

    /// <summary>
    /// Decodes one tile of a codestream to a PNG.
    /// </summary>
    /// <param name="sourceFileName">Name of the codestream in the input folder.</param>
    /// <param name="description">Text naming this variant on the console.</param>
    /// <param name="outputFileName">Name of the PNG written to the output folder.</param>
    /// <param name="tileIndex">Index of the tile, in row-major order.</param>
    private static void DecodeTile(string sourceFileName, string description, string outputFileName, int tileIndex)
    {
        Console.WriteLine($"[Jpx] Decoding {sourceFileName} {description}...");

        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, sourceFileName));

        // Parses the main header: image and tile grid, coding style, quantization, and components.
        JpxHeader header = JpxHeader.Parse(fileBytes);

        // The tile reader decodes only the tiles it is asked for.
        JpxTileReader tileReader = new(header, fileBytes.AsMemory(header.CodestreamOffset));

        // The samples the tile covers, at the decoded resolution.
        JpxRectangle bounds = tileReader.GetTileBounds(tileIndex);

        // Samples arrive bit-packed at the codestream's own precision, one byte per component here.
        int rowBytes = tileReader.GetRowBytes(bounds.Width);
        var samples = new byte[rowBytes * bounds.Height];

        // Writes the tile's rows from the first sample column of the buffer.
        tileReader.ReadTile(tileIndex, samples, rowBytes, sampleOffset: 0);

        PngImageInfo pngInfo = new() { Width = bounds.Width, Height = bounds.Height, BitDepth = 8, ColorType = PngColorType.Truecolor };
        string outputPath = ExamplePaths.Output(Format, outputFileName);
        using FileStream output = File.Create(outputPath);
        PngWriter.Write(output, pngInfo, samples, rowBytes, adaptiveFiltering: true, CompressionLevel.Optimal);

        Console.WriteLine($"[Jpx]   {bounds.Width}x{bounds.Height} -> {ExamplePaths.Relative(outputPath)}");
    }
}
