using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Models;

namespace PdfPixel.ResourceGenerator.Encodings;

/// <summary>
/// Generates CID-to-Unicode binary blobs from the Adobe mapping-resources-pdf pdf2unicode CMap files.
/// </summary>
internal static class CidToUnicodeFromPdfCMapGenerator
{
    public static void GenerateAll(string sourceRoot, string outputDirectory, ILoggerFactory loggerFactory)
    {
        string pdf2UnicodeDirectory = Path.Combine(sourceRoot, "pdf2unicode");
        if (!Directory.Exists(pdf2UnicodeDirectory))
        {
            Console.WriteLine($"  WARNING: pdf2unicode directory not found at {pdf2UnicodeDirectory}");
            return;
        }

        Directory.CreateDirectory(outputDirectory);

        foreach (string filePath in Directory.GetFiles(pdf2UnicodeDirectory))
        {
            if (!string.IsNullOrEmpty(Path.GetExtension(filePath)))
            {
                continue;
            }

            string name = Path.GetFileName(filePath);
            string outputPath = Path.Combine(outputDirectory, $"{name}.bin");

            Console.WriteLine($"  Generating: {outputPath}");

            ReadOnlyMemory<byte> bytes = File.ReadAllBytes(filePath);
            PdfCMap? cmap = PdfCMapParser.ParseCMap(bytes, loggerFactory, _ => null);

            if (cmap == null)
            {
                Console.WriteLine($"    -> skipped (parse failed)");
                continue;
            }

            Dictionary<uint, string> cidToUnicode = BuildCidToUnicode(cmap);

            if (cidToUnicode.Count == 0)
            {
                Console.WriteLine($"    -> skipped (no entries)");
                continue;
            }

            byte[] blob = TextResourceSerializer.GenerateCidToUnicodeMapBlob(cidToUnicode);
            File.WriteAllBytes(outputPath, blob);
            Console.WriteLine($"    -> {cidToUnicode.Count} entries");
        }
    }

    private static Dictionary<uint, string> BuildCidToUnicode(PdfCMap cmap)
    {
        Dictionary<uint, string> result = [];

        for (int len = 1; len <= 4; len++)
        {
            List<UnicodeRangeMap>? ranges = cmap.GetUnicodeRanges(len);
            if (ranges != null)
            {
                foreach (UnicodeRangeMap range in ranges)
                {
                    for (uint code = range.Start; code <= range.End; code++)
                    {
                        int codePoint = range.StartUnicode + (int)(code - range.Start);
                        if (PdfCMap.IsValidCodePoint(codePoint))
                        {
                            result[code] = char.ConvertFromUtf32(codePoint);
                        }
                    }
                }
            }
        }

        foreach (KeyValuePair<PdfCharacterCode, string> kvp in cmap.CodeToUnicode)
        {
            if (kvp.Key.Length > 0)
            {
                uint code = PdfCharacterCode.UnpackBigEndianToUInt(kvp.Key.Bytes.Span);
                result[code] = kvp.Value;
            }
        }

        return result;
    }
}
