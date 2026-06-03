using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using PdfPixel.Models;

namespace PdfPixel.ResourceGenerator.Encodings;

/// <summary>
/// Downloads and generates Adobe Glyph List binary blobs.
/// </summary>
internal static class AglGenerator
{
    public static async Task GenerateAsync(string aglUrl, string zapfDingbatsUrl, string workDirectory, string outputDirectory)
    {
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(outputDirectory);

        using HttpClient httpClient = new();

        await GenerateOneAsync(httpClient, aglUrl, workDirectory, outputDirectory).ConfigureAwait(false);
        await GenerateOneAsync(httpClient, zapfDingbatsUrl, workDirectory, outputDirectory).ConfigureAwait(false);
    }

    private static async Task GenerateOneAsync(HttpClient httpClient, string url, string workDirectory, string outputDirectory)
    {
        string sourceFileName = Path.GetFileName(new Uri(url).LocalPath);
        string outputFileName = Path.ChangeExtension(sourceFileName, ".bin");
        string localPath = Path.Combine(workDirectory, sourceFileName);

        Console.WriteLine($"  Downloading {url} ...");
        string content = await httpClient.GetStringAsync(new Uri(url)).ConfigureAwait(false);
        await File.WriteAllTextAsync(localPath, content).ConfigureAwait(false);

        Dictionary<PdfString, string> characterMap = ParseGlyphList(content);
        Console.WriteLine($"  {outputFileName}: {characterMap.Count} entries");

        byte[] blob = TextResourceSerializer.GenerateCharacterMapBlob(characterMap);
        File.WriteAllBytes(Path.Combine(outputDirectory, outputFileName), blob);
    }

    public static void GenerateFromFile(string txtPath, string outputDirectory)
    {
        string content = File.ReadAllText(txtPath);
        string outputFileName = Path.ChangeExtension(Path.GetFileName(txtPath), ".bin");
        string outputPath = Path.Combine(outputDirectory, outputFileName);

        Dictionary<PdfString, string> characterMap = ParseGlyphList(content);
        Console.WriteLine($"  {outputFileName}: {characterMap.Count} entries");

        byte[] blob = TextResourceSerializer.GenerateCharacterMapBlob(characterMap);
        File.WriteAllBytes(outputPath, blob);
    }

    private static Dictionary<PdfString, string> ParseGlyphList(string content)
    {
        Dictionary<PdfString, string> result = [];

        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            int semicolon = trimmed.IndexOf(';');
            if (semicolon < 0)
            {
                continue;
            }

            string glyphName = trimmed.Substring(0, semicolon).Trim();
            string unicodePart = trimmed.Substring(semicolon + 1).Trim();

            string unicode = ParseUnicodeHex(unicodePart);
            if (unicode.Length == 0)
            {
                continue;
            }

            result[PdfString.FromString(glyphName)] = unicode;
        }

        return result;
    }

    private static string ParseUnicodeHex(string unicodePart)
    {
        string[] tokens = unicodePart.Split(' ');
        System.Text.StringBuilder sb = new();

        foreach (string token in tokens)
        {
            string hex = token.Trim();
            if (hex.Length == 0)
            {
                continue;
            }

            if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
            {
                return string.Empty;
            }

            sb.Append(char.ConvertFromUtf32(codePoint));
        }

        return sb.ToString();
    }
}
