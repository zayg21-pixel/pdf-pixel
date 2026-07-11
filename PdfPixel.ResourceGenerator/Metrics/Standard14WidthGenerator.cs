using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PdfPixel.ResourceGenerator.Metrics;

/// <summary>
/// Generates per-variant binary width blobs for the Standard 14 fonts from <see cref="Standard14MetricsData"/>.
/// Monospace variants (Courier family) are skipped since every glyph shares a single fixed width.
/// </summary>
internal static class Standard14WidthGenerator
{
    public static void GenerateAll(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach (KeyValuePair<string, IReadOnlyDictionary<string, int>> entry in Standard14MetricsData.GlyphWidths)
        {
            string outputFileName = ToPascalCase(entry.Key) + "Width.bin";
            byte[] blob = GenerateWidthBlob(entry.Value);

            Console.WriteLine($"  {outputFileName}: {entry.Value.Count} entries");
            File.WriteAllBytes(Path.Combine(outputDirectory, outputFileName), blob);
        }
    }

    /// <summary>
    /// Generates a binary blob representing a glyph-name-to-width map.
    /// Format per entry: [UTF-8 name bytes][0x00 terminator][2 bytes width, ushort, little-endian]
    /// </summary>
    private static byte[] GenerateWidthBlob(IReadOnlyDictionary<string, int> glyphWidths)
    {
        using MemoryStream stream = new();

        foreach (KeyValuePair<string, int> entry in glyphWidths)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Key);

            if (entry.Value is < 0 or > ushort.MaxValue)
            {
                throw new InvalidOperationException($"Width for glyph '{entry.Key}' does not fit in a ushort: {entry.Value}");
            }

            stream.Write(nameBytes, 0, nameBytes.Length);
            stream.WriteByte(0);
            stream.WriteByte((byte)(entry.Value & 0xFF));
            stream.WriteByte((byte)((entry.Value >> 8) & 0xFF));
        }

        return stream.ToArray();
    }

    private static string ToPascalCase(string variantName)
    {
        return variantName
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("Oblique", "Italic", StringComparison.Ordinal);
    }
}
