using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfPixel.ResourceGenerator.Encodings;

/// <summary>
/// Parser for Adobe cid2code TSV tables into CID-to-Unicode dictionaries.
/// Lines starting with '#' are comments. First non-comment line is the header row.
/// </summary>
internal static class Cid2CodeTableParser
{
    /// <summary>
    /// Parse a cid2code table file into a jagged array of strings.
    /// Row 0 contains headers; rows 1..N contain data rows.
    /// </summary>
    public static string[][] ParseTable(string filePath)
    {
        List<string[]> rows = [];

        using StreamReader reader = new(filePath);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
            {
                continue;
            }

            rows.Add(line.Split('\t').Select(s => s.Trim()).ToArray());
        }

        return rows.ToArray();
    }

    /// <summary>
    /// Build a CID-to-Unicode dictionary from a parsed cid2code table.
    /// Columns are tried in priority order: UTF-16 first, then UTF-32, then UTF-8.
    /// Vertical variant tokens (ending in 'v') are used only as last resort.
    /// </summary>
    /// <param name="codeTable">Parsed table where row 0 is headers and rows 1..N are data.</param>
    /// <param name="utf8Columns">UTF-8 column indices, tried last.</param>
    /// <param name="utf16Columns">UTF-16 column indices, tried first.</param>
    /// <param name="utf32Columns">UTF-32 column indices, tried second.</param>
    public static Dictionary<uint, string> BuildCidToUnicode(string[][] codeTable, int[] utf8Columns, int[] utf16Columns, int[] utf32Columns)
    {
        if (codeTable == null)
        {
            throw new ArgumentNullException(nameof(codeTable));
        }

        if (utf16Columns == null || utf16Columns.Length == 0)
        {
            throw new ArgumentException("utf16Columns must not be null or empty.", nameof(utf16Columns));
        }

        Dictionary<uint, string> result = [];

        for (int rowIndex = 1; rowIndex < codeTable.Length; rowIndex++)
        {
            string[] row = codeTable[rowIndex];
            if (!uint.TryParse(row[0].Trim(), out uint cid))
            {
                continue;
            }

            string unicode = TryDecodeColumns(row, utf16Columns, 16);

            if (unicode.Length == 0)
            {
                unicode = TryDecodeColumns(row, utf32Columns, 32);
            }

            if (unicode.Length == 0)
            {
                unicode = TryDecodeColumns(row, utf8Columns, 8);
            }

            if (unicode.Length > 0)
            {
                result[cid] = unicode;
            }
        }

        return result;
    }

    private static string TryDecodeColumns(string[] row, int[] columnIndices, int utfBits)
    {
        foreach (int colIndex in columnIndices)
        {
            if (colIndex >= row.Length)
            {
                continue;
            }

            string decoded = DecodeUnicodeHexSequence(row[colIndex], utfBits);
            if (decoded.Length > 0)
            {
                return decoded;
            }
        }

        return string.Empty;
    }

    private static string DecodeUnicodeHexSequence(string raw, int utfBits)
    {
        string[] allTokens = raw.Split(',').Select(t => t.Trim()).ToArray();

        // Prefer non-vertical tokens, order by code value (ascending)
        string? token = allTokens
            .Where(t => !t.EndsWith("v", StringComparison.Ordinal))
            .OrderBy(t => t)
            .FirstOrDefault();

        // Fall back to vertical tokens if nothing else available
        token ??= allTokens
            .Where(t => t.EndsWith("v", StringComparison.Ordinal))
            .Select(t => t.Substring(0, t.Length - 1))
            .OrderBy(t => t)
            .FirstOrDefault();

        if (token == null || token == "*" || token.Length % 2 != 0)
        {
            return string.Empty;
        }

        int totalBytes = token.Length / 2;
        var buffer = new byte[totalBytes];

        for (int i = 0; i < totalBytes; i++)
        {
            if (!byte.TryParse(token.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out buffer[i]))
            {
                return string.Empty;
            }
        }

        return utfBits switch
        {
            8 => System.Text.Encoding.UTF8.GetString(buffer),
            16 => System.Text.Encoding.BigEndianUnicode.GetString(buffer),
            32 => System.Text.Encoding.UTF32.GetString(buffer),
            _ => throw new NotSupportedException($"Unsupported UTF bit size: {utfBits}.")
        };
    }
}
