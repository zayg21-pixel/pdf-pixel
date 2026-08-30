using System;
using System.IO;
using PdfPixel.Ccitt;

namespace PdfPixel.Examples;

/// <summary>
/// Decoding a CCITT Group 4 fax stream to a bi-level image.
/// </summary>
internal static class CcittExamples
{
    private const string Format = "Ccitt";
    private const string SourceFile = "baboon-g4.ccitt";

    // A CCITT stream carries no dimensions of its own. In a PDF they come from the image
    // dictionary, together with the CCITTFaxDecode parameters below.
    private const int Width = 512;
    private const int Height = 512;

    /// <summary>
    /// Runs every CCITT example.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine($"[Ccitt] Decoding {SourceFile}...");

        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, SourceFile));

        // K selects the coding: 0 is Group 3 one-dimensional, negative is Group 4, and a positive
        // value is Group 3 mixed with K rows between one-dimensional lines. The remaining flags
        // are the /DecodeParms entries of the same name, here left at their PDF defaults.
        CcittRowDecoder decoder = new(
            fileBytes,
            Width,
            Height,
            blackIs1: false,
            k: -1,
            endOfLine: false,
            byteAlign: false,
            endOfBlock: true);

        // Rows come out packed to 1 bit per pixel, most significant bit first.
        var rows = new byte[decoder.RowStride * Height];

        for (int row = 0; row < Height && decoder.DecodeNextRow(rows.AsSpan(row * decoder.RowStride, decoder.RowStride)); row++)
        {
        }

        // With BlackIs1 off a clear bit is black, which is what PNG reads too.
        string outputPath = ExamplePaths.Output(Format, "baboon-g4.png");
        PngWriter.Write(outputPath, Width, Height, bitDepth: 1, PngColorType.Gray, decoder.RowStride, rows);

        Console.WriteLine($"[Ccitt]   {Width}x{Height}, {decoder.RowsDecoded} row(s) -> {ExamplePaths.Relative(outputPath)}");
    }
}
