using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PdfPixel.ResourceGenerator.Encodings;

/// <summary>
/// Generates CID-to-Unicode binary blobs for the well-known non-embedded standard font glyph
/// layouts (ported from pdf.js's standard_fonts.js, Apache License 2.0, Mozilla Foundation),
/// from the plain-text "cid codepoint" source lists checked into this project.
/// </summary>
internal static class StandardFontGlyphMapGenerator
{
    public static void GenerateAll(string sourceDirectory, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        GenerateOne(Path.Combine(sourceDirectory, "StandardFontGlyphMap.txt"), outputDirectory, "StandardFontGlyphMap.bin");
        GenerateOne(Path.Combine(sourceDirectory, "StandardFontGlyphMapArialBlack.txt"), outputDirectory, "StandardFontGlyphMapArialBlack.bin");
        GenerateOne(Path.Combine(sourceDirectory, "StandardFontGlyphMapCalibri.txt"), outputDirectory, "StandardFontGlyphMapCalibri.bin");
    }

    private static void GenerateOne(string sourcePath, string outputDirectory, string outputFileName)
    {
        Dictionary<uint, string> cidToUnicode = [];

        foreach (string line in File.ReadAllLines(sourcePath))
        {
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split(' ');
            var cid = uint.Parse(parts[0], CultureInfo.InvariantCulture);
            var codepoint = int.Parse(parts[1], CultureInfo.InvariantCulture);
            cidToUnicode[cid] = char.ConvertFromUtf32(codepoint);
        }

        byte[] blob = TextResourceSerializer.GenerateCidToUnicodeMapBlob(cidToUnicode);
        string outputPath = Path.Combine(outputDirectory, outputFileName);

        Console.WriteLine($"  {outputFileName}: {cidToUnicode.Count} entries");
        File.WriteAllBytes(outputPath, blob);
    }
}
