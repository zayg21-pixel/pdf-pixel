using PdfReader.Models;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Management
{
    /// <summary>
    /// Parsed representation of a PDF BaseFont PostScript name.
    /// Performs syntactic normalization (subset removal, manufacturer suffix removal, style token detection)
    /// and exposes a normalized stem (family stem without style tokens or trailing MT).
    /// </summary>
    public sealed class PdfFontName
    {
        private static readonly string[] StyleTokensOrdered =
        {
            "BoldItalic",
            "BoldOblique",
            "Bold",
            "Italic",
            "Oblique"
        };

        /// <summary>
        /// Raw BaseFont value from the PDF (may include subset prefix).
        /// </summary>
        public PdfString RawName { get; }

        /// <summary>
        /// Subset tag (e.g., ABCDEF) if present before '+'. Null if not subsetted.
        /// </summary>
        public string SubsetTag { get; }

        /// <summary>
        /// Stem after removing subset tag, trailing manufacturer token (MT), and style tokens.
        /// </summary>
        public string NormalizedStem { get; }

        /// <summary>
        /// Style hint parsed from PostScript name tokens (does not inspect font descriptors).
        /// </summary>
        public bool BoldHint { get; }

        /// <summary>
        /// Style hint parsed from PostScript name tokens (does not inspect font descriptors).
        /// </summary>
        public bool ItalicHint { get; }

        private PdfFontName(
            PdfString rawName,
            string subsetTag,
            string normalizedStem,
            bool boldHint,
            bool italicHint)
        {
            RawName = rawName;
            SubsetTag = subsetTag;
            NormalizedStem = normalizedStem;
            BoldHint = boldHint;
            ItalicHint = italicHint;
        }

        /// <summary>
        /// Parse a raw PDF BaseFont name into a <see cref="PdfFontName"/>.
        /// </summary>
        /// <param name="rawName">Raw BaseFont string (may be null or empty).</param>
        public static PdfFontName Parse(PdfString rawName)
        {
            if (rawName.IsEmpty)
            {
                return new PdfFontName(PdfString.Empty, null, string.Empty, false, false);
            }

            string working = rawName.ToString();
            string subsetTag = null;

            int plusIndex = working.IndexOf('+');
            if (plusIndex > 0 && plusIndex < working.Length - 1)
            {
                subsetTag = working.Substring(0, plusIndex);
                working = working.Substring(plusIndex + 1);
            }

            if (working.EndsWith("MT", StringComparison.Ordinal))
            {
                working = working.Substring(0, working.Length - 2);
            }

            string basePart = working;
            string stylePart = null;
            int hyphenIndex = working.IndexOf('-');
            if (hyphenIndex > 0 && hyphenIndex < working.Length - 1)
            {
                basePart = working.Substring(0, hyphenIndex);
                stylePart = working.Substring(hyphenIndex + 1);
            }

            bool bold = false;
            bool italic = false;

            if (!string.IsNullOrEmpty(stylePart))
            {
                string lowered = stylePart.ToLowerInvariant();
                if (lowered.Contains("bold"))
                {
                    bold = true;
                }
                if (lowered.Contains("italic") || lowered.Contains("oblique"))
                {
                    italic = true;
                }
            }

            if (stylePart == null)
            {
                string temp = basePart;
                foreach (string token in StyleTokensOrdered)
                {
                    if (temp.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                    {
                        string lowered = token.ToLowerInvariant();
                        if (lowered.Contains("bold"))
                        {
                            bold = true;
                        }
                        if (lowered.Contains("italic") || lowered.Contains("oblique"))
                        {
                            italic = true;
                        }
                        temp = temp.Substring(0, temp.Length - token.Length);
                        break;
                    }
                }
                basePart = temp;
            }

            return new PdfFontName(rawName, subsetTag, basePart, bold, italic);
        }
    }
}
