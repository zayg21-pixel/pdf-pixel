using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PdfPixel.ResourceGenerator.Fonts;

/// <summary>
/// Reads the CharMetrics section of an Adobe AFM file.
/// </summary>
internal static class AfmParser
{
    /// <summary>
    /// Parses every entry between StartCharMetrics and EndCharMetrics of the AFM file at
    /// <paramref name="path"/>, in file order. An entry without an N field is skipped.
    /// </summary>
    public static IReadOnlyList<AfmCharacterMetric> Parse(string path)
    {
        List<AfmCharacterMetric> characters = [];
        var isInCharMetrics = false;

        foreach (string line in File.ReadLines(path))
        {
            if (line.StartsWith("StartCharMetrics", StringComparison.Ordinal))
            {
                isInCharMetrics = true;
                continue;
            }

            if (line.StartsWith("EndCharMetrics", StringComparison.Ordinal))
            {
                break;
            }

            if (!isInCharMetrics)
            {
                continue;
            }

            AfmCharacterMetric? character = ParseCharacterLine(line);
            if (character != null)
            {
                characters.Add(character.Value);
            }
        }

        return characters;
    }

    /// <summary>
    /// Parses one "C code ; WX width ; N name ; B llx lly urx ury ;" line. Returns null when the line
    /// states no glyph name.
    /// </summary>
    private static AfmCharacterMetric? ParseCharacterLine(string line)
    {
        int code = -1;
        int width = 0;
        string? name = null;

        foreach (string field in line.Split(';'))
        {
            string[] parts = field.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            if (parts[0] == "C")
            {
                code = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            else if (parts[0] == "WX")
            {
                width = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            else if (parts[0] == "N")
            {
                name = parts[1];
            }
        }

        if (name == null)
        {
            return null;
        }

        return new AfmCharacterMetric(code, name, width);
    }
}
