using System;
using System.IO;
using System.Linq;
using PdfPixel.Models;

namespace PdfPixel.ResourceGenerator.Encodings;

/// <summary>
/// Generates the binary blob for the standard Macintosh glyph ordering used by TrueType 'post'
/// table formats 1.0 and 2.0, from the plain-text source list in PostGlyphOrder.txt.
/// </summary>
internal static class PostGlyphOrderGenerator
{
    private const int ExpectedGlyphCount = 258;

    public static void Generate(string sourcePath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        string[] lines = File.ReadAllLines(sourcePath);
        PdfString[] names = lines
            .Where(line => line.Length > 0)
            .Select(line => (PdfString)line)
            .ToArray();

        if (names.Length != ExpectedGlyphCount)
        {
            throw new InvalidOperationException($"Expected {ExpectedGlyphCount} standard Macintosh glyph names, found {names.Length}.");
        }

        byte[] blob = TextResourceSerializer.GeneratePdfStringBlob(names);
        string outputPath = Path.Combine(outputDirectory, "PostGlyphOrder.bin");

        Console.WriteLine($"  PostGlyphOrder.bin: {names.Length} entries");
        File.WriteAllBytes(outputPath, blob);
    }
}
