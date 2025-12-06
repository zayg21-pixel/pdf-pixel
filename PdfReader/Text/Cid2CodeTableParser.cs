using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PdfReader.Text
{
    /// <summary>
    /// Minimal parser for cid2code TSV tables into a jagged array.
    /// - Lines starting with '#' are comments and skipped.
    /// - Columns are separated by a tab character ('\t').
    /// - New lines are CRLF ("\r\n").
    /// The first non-comment line is treated as the header row; subsequent lines are data rows.
    /// </summary>
    public static class Cid2CodeTableParser
    {
        /// <summary>
        /// Parse a cid2code table into a jagged array of strings.
        /// Row 0 contains headers; rows 1..N contain values.
        /// </summary>
        /// <param name="data">Table content as raw bytes using tab-separated columns and CRLF new lines.</param>
        /// <returns>Jagged array of rows. Row 0 is headers; rows 1..N are data rows.</returns>
        public static string[][] ParseTable(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var rows = new List<string[]>();

            using (var stream = new MemoryStream(data))
            using (var reader = new StreamReader(stream))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (IsComment(line))
                    {
                        continue;
                    }

                    var columns = SplitColumns(line)
                        .Select(s => (s ?? string.Empty).Trim())
                        .ToArray();

                    rows.Add(columns);
                }
            }

            return rows.ToArray();
        }

        /// <summary>
        /// Build a ToUnicode CMap dictionary from a parsed table.
        /// Assumes column 0 contains the CID as plain hex string (no prefixes),
        /// and utf16Columns contains indices of possible UTF-16 code columns.
        /// UTF-16 values must be a single hex code point (e.g., "4E00")
        /// or a comma-separated sequence of hex code points (e.g., "0041,0301").
        /// </summary>
        /// <param name="codeTable">Jagged array where row 0 is headers and rows 1..N are data.</param>
        /// <param name="utf8Columns">Indices of the UTF-8 columns in each data row (searched in order).</param>
        /// <param name="utf16Columns">Indices of the UTF-16 columns in each data row (searched in order).</param>
        /// <param name="utf32Columns">Indices of the UTF-32 columns in each data row (searched in order).</param>
        /// <returns>Dictionary mapping CID to Unicode string.</returns>
        public static Dictionary<uint, string> ToUnicodeCMap(string[][] codeTable, int[] utf8Columns, int[] utf16Columns, int[] utf32Columns)
        {
            // columns for Japan: [19, 22], [20, 23, 17, 18], [21, 24, 25, 26]
            // columns for CSN: [9], [10, 8], [11]
            // columns for GB: [11], [12, 10], [13]
            // columns for Korea: [8], [9, 7], [10]
            // columns for KR/Manga: [1], [2], [3]
            if (codeTable == null)
            {
                throw new ArgumentNullException(nameof(codeTable));
            }
            if (utf16Columns == null || utf16Columns.Length == 0)
            {
                throw new ArgumentException("utf16Columns must not be null or empty.", nameof(utf16Columns));
            }

            var result = new Dictionary<uint, string>();

            for (int rowIndex = 1; rowIndex < codeTable.Length; rowIndex++)
            {
                var row = codeTable[rowIndex];
                var cidText = row[0].Trim();
                var cid = uint.Parse(cidText);

                string unicode = string.Empty;

                foreach (var colIndex in utf16Columns)
                {
                    if (colIndex < row.Length)
                    {
                        var utf16Raw = row[colIndex];
                        unicode = DecodeUnicodeHexSequence(utf16Raw, 16);
                        if (!string.IsNullOrEmpty(unicode))
                        {
                            break;
                        }
                    }
                }

                if (unicode.Length == 0)
                {
                    // Try UTF-32 columns if no UTF-16 mapping found
                    foreach (var colIndex in utf32Columns)
                    {
                        if (colIndex < row.Length)
                        {
                            var utf32Raw = row[colIndex];
                            unicode = DecodeUnicodeHexSequence(utf32Raw, 32);
                            if (!string.IsNullOrEmpty(unicode))
                            {
                                break;
                            }
                        }
                    }
                }

                if (unicode.Length == 0)
                {
                    // Try UTF-8 columns if no UTF-32 mapping found
                    foreach (var colIndex in utf8Columns)
                    {
                        if (colIndex < row.Length)
                        {
                            var utf8Raw = row[colIndex];
                            unicode = DecodeUnicodeHexSequence(utf8Raw, 8);
                            if (!string.IsNullOrEmpty(unicode))
                            {
                                break;
                            }
                        }
                    }
                }

                if (unicode.Length > 0)
                {
                    result[cid] = unicode;
                }
            }

            return result;
        }

        private static bool IsComment(string line)
        {
            return line.Length > 0 && line[0] == '#';
        }

        private static string[] SplitColumns(string line)
        {
            return line.Split('\t');
        }

        private static string DecodeUnicodeHexSequence(string utfRaw, int uftBytes)
        {
            var allTokens = utfRaw.Split(',').Select(t => t.Trim()).ToArray();

            string token = allTokens
                .Where(t => !t.EndsWith("v", StringComparison.Ordinal))
                .OrderBy(x => x) // Order by min byte code
                .FirstOrDefault();

            token ??= allTokens
                    .Where(t => t.EndsWith("v", StringComparison.Ordinal))
                    .Select(t => t.Substring(0, t.Length - 1)) // Remove 'v' suffix
                    .OrderBy(x => x) // Order by min byte code
                    .FirstOrDefault();

            if (token == null)
            {
                return string.Empty;
            }

            if (token == "*")
            {
                return string.Empty;
            }

            // Will require surrogate pairs or multi-byte sequences, skip as it will be parsed as UTF-32.
            if (token.Length % 2 != 0)
            {
                return string.Empty;
            }

            var totalBytes = token.Length / 2;
            var buffer = new byte[totalBytes];
            int offset = 0;

            int tokenLen = token.Length;
            for (int b = 0; b < tokenLen; b += 2)
            {
                buffer[offset++] = byte.Parse(token.Substring(b, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            }

            switch (uftBytes)
            {
                case 8:
                    return System.Text.Encoding.UTF8.GetString(buffer);
                case 16:
                    return System.Text.Encoding.BigEndianUnicode.GetString(buffer);
                case 32:
                    return System.Text.Encoding.UTF32.GetString(buffer);
                default:
                    throw new NotSupportedException($"Unsupported UTF byte size: {uftBytes}.");
            }
        }
    }
}
