using System;
using System.IO;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Examples;

/// <summary>
/// Decoding a JBIG2 bi-level image to a bitmap.
/// </summary>
internal static class Jbig2Examples
{
    private const string Format = "Jbig2";
    private const string SourceFile = "baboon.jb2";

    /// <summary>
    /// Runs every JBIG2 example.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine($"[Jbig2] Decoding {SourceFile}...");

        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, SourceFile));
        Jbig2PageDecoder decoder = new();

        // Inside a PDF the /JBIG2Globals stream holds the shared symbol and pattern dictionaries;
        // decode it with DecodeGlobalCache and pass the result as the globalCache argument. A
        // standalone file such as this one carries its own segments, so there are no globals.
        //
        // The expected width and height come from the image dictionary in a PDF; the page
        // information segment of a standalone file overrides them, so zero is enough here.
        Jbig2Bitmap bitmap = decoder.Decode(fileBytes, expectedWidth: 0, expectedHeight: 0);

        // The bitmap holds packed 1-bit rows, most significant bit first. JBIG2 bitmaps are
        // inverted by design, a set bit being black, so the rows are flipped before they are written.
        ReadOnlySpan<byte> bitmapData = bitmap.ReadOnlyData;
        var rows = new byte[bitmapData.Length];

        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = (byte)~bitmapData[index];
        }

        string outputPath = ExamplePaths.Output(Format, "baboon.png");
        PngWriter.Write(outputPath, bitmap.Width, bitmap.Height, bitDepth: 1, PngColorType.Gray, bitmap.Stride, rows);

        Console.WriteLine($"[Jbig2]   {bitmap.Width}x{bitmap.Height} -> {ExamplePaths.Relative(outputPath)}");
    }
}
