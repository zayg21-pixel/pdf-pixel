using System;
using System.Collections.Generic;
using System.IO;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;

namespace PdfPixel.Examples;

/// <summary>
/// Decoding JPEG 2000 codestreams: at full resolution, at a reduced resolution, and over a
/// region of interest.
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
        Decode(SourceFile, "at full resolution", "baboon.png", descaleFactor: 1, regionOfInterest: null);

        // The descale factor must be a power of two. Above 1 the inverse wavelet transform skips
        // its finest levels, which is faster than decoding in full and scaling down afterwards.
        Decode(SourceFile, "at a quarter of its resolution", "baboon-descale-4.png", descaleFactor: 4, regionOfInterest: null);

        // A region of interest is given in full-resolution coordinates and skips whole tiles, so it
        // only pays off on a tiled codestream. This file carries a grid of 256x256 tiles.
        JpxRectangle topLeftTile = new(0, 0, 256, 256);
        Decode(TiledSourceFile, "over one region of interest", "baboon-region.png", descaleFactor: 1, topLeftTile);

        // A raw codestream carries no boxes, so opacity needs the JP2 wrapper: its cdef box marks
        // one component as alpha. JpxDecodingParameters can drop that component with
        // includeOpacityComponent, which then never reaches the entropy decoder at all.
        Decode(AlphaSourceFile, "with its alpha channel", "baboon-alpha.png", descaleFactor: 1, regionOfInterest: null);
    }

    /// <summary>
    /// Decodes a codestream to a PNG.
    /// </summary>
    /// <param name="sourceFileName">Name of the codestream in the input folder.</param>
    /// <param name="description">Text naming this variant on the console.</param>
    /// <param name="outputFileName">Name of the PNG written to the output folder.</param>
    /// <param name="descaleFactor">Power-of-two reduction applied to the decoded resolution.</param>
    /// <param name="regionOfInterest">Region to decode, or null for the whole image.</param>
    private static void Decode(
        string sourceFileName,
        string description,
        string outputFileName,
        int descaleFactor,
        JpxRectangle? regionOfInterest)
    {
        Console.WriteLine($"[Jpx] Decoding {sourceFileName} {description}...");

        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, sourceFileName));

        // Parses the main header: image and tile grid, coding style, quantization, and components.
        JpxHeader header = JpxReader.ParseHeader(fileBytes);

        IReadOnlyList<JpxRectangle>? regions = (regionOfInterest == null)
            ? null
            : new List<JpxRectangle> { regionOfInterest.Value };
        JpxDecodingParameters decodingParameters = new(descaleFactor, regions);

        // The tile provider decodes tiles on demand from the codestream that follows the header.
        JpxTileProvider tileProvider = new(header, fileBytes.AsSpan(header.CodestreamOffset), decodingParameters);

        // The converter assembles the tile grid into image rows, decoding each tile row as it is reached.
        JpxTileToRowConverter converter = new(header, tileProvider, decodingParameters);

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
        string outputPath = ExamplePaths.Output(Format, outputFileName);
        PngWriter.Write(outputPath, converter.Width, converter.Height, bitDepth: 8, colorType, rowBytes, samples);

        Console.WriteLine($"[Jpx]   {converter.Width}x{converter.Height} -> {ExamplePaths.Relative(outputPath)}");
    }
}
