using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.ResourceGenerator.Encodings;

/// <summary>
/// Generates CID-to-Unicode binary blobs from Adobe cid2code.txt files.
/// </summary>
internal static class CidToUnicodeGenerator
{
    private static readonly Dictionary<string, (int[] Utf8, int[] Utf16, int[] Utf32)> CollectionColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Adobe-Japan1",  ([19, 22],     [20, 23, 17, 18], [21, 24, 25, 26]) },
        { "Adobe-CNS1",    ([9],          [10, 8],          [11]) },
        { "Adobe-GB1",     ([11],         [12, 10],         [13]) },
        { "Adobe-Korea1",  ([8],          [9, 7],           [10]) },
        { "Adobe-KR",      ([1],          [2],              [3]) },
        { "Adobe-Manga1",  ([1],          [2],              [3]) },
        { "Adobe-Japan2",  ([1],          [2],              [3]) }
    };

    public static void GenerateAll(string sourceRoot, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach (string filePath in Directory.GetFiles(sourceRoot, "cid2code.txt", SearchOption.AllDirectories))
        {
            string folderName = new DirectoryInfo(Path.GetDirectoryName(filePath)!).Name;
            string outputPath = Path.Combine(outputDirectory, $"{folderName}.bin");

            Console.WriteLine($"  Generating: {outputPath}");

            (int[] utf8, int[] utf16, int[] utf32) = FindColumns(folderName);

            string[][] table = Cid2CodeTableParser.ParseTable(filePath);
            Dictionary<uint, string> cidToUnicode = Cid2CodeTableParser.BuildCidToUnicode(table, utf8, utf16, utf32);

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

    private static (int[] Utf8, int[] Utf16, int[] Utf32) FindColumns(string folderName)
    {
        foreach (KeyValuePair<string, (int[] Utf8, int[] Utf16, int[] Utf32)> entry in CollectionColumns)
        {
            if (folderName.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        Console.WriteLine($"    -> WARNING: unknown collection '{folderName}', using KR/Manga column defaults");
        return ([1], [2], [3]);
    }
}
