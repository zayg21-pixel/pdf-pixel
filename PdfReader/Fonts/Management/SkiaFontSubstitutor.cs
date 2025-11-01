using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Management
{
    /// <summary>
    /// Utility class for PDF font substitution and style detection.
    /// Used by PdfFontCache for non-embedded font fallback and family matching.
    /// </summary>
    internal static class SkiaFontSubstitutor
    {
        private static readonly Dictionary<string, string[]> MergedFamilyMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Times", new[] { "Times New Roman", "Times", "Liberation Serif", "Nimbus Roman" } },
            { "TimesNewRoman", new[] { "Times New Roman", "Times", "Liberation Serif", "Nimbus Roman" } },
            { "TimesNewRomanPS", new[] { "Times New Roman", "Times", "Liberation Serif", "Nimbus Roman" } },
            { "Helvetica", new[] { "Helvetica", "Arial", "Liberation Sans", "Nimbus Sans" } },
            { "Arial", new[] { "Arial", "Helvetica", "Liberation Sans", "Nimbus Sans" } },
            { "Courier", new[] { "Courier New", "Courier", "Liberation Mono", "Nimbus Mono" } },
            { "CourierNew", new[] { "Courier New", "Courier", "Liberation Mono", "Nimbus Mono" } },
            { "CourierNewPS", new[] { "Courier New", "Courier", "Liberation Mono", "Nimbus Mono" } },
            { "Symbol", new[] { "Symbol", "Segoe UI Symbol" } },
            { "ZapfDingbats", new[] { "Zapf Dingbats", "Segoe UI Symbol", "Wingdings" } },
            { "LiberationSans", new[] { "Liberation Sans", "Helvetica", "Arial", "Nimbus Sans" } },
            { "LiberationSerif", new[] { "Liberation Serif", "Times New Roman", "Times", "Nimbus Roman" } },
            { "LiberationMono", new[] { "Liberation Mono", "Courier New", "Courier", "Nimbus Mono" } },
            { "DejaVuSans", new[] { "DejaVu Sans", "Liberation Sans", "Arial", "Helvetica" } },
            { "DejaVuSerif", new[] { "DejaVu Serif", "Liberation Serif", "Times New Roman", "Times" } },
            { "DejaVuSansMono", new[] { "DejaVu Sans Mono", "Liberation Mono", "Courier New", "Courier" } },
            { "NimbusSans", new[] { "Nimbus Sans", "Helvetica", "Arial", "Liberation Sans" } },
            { "NimbusRoman", new[] { "Nimbus Roman", "Times New Roman", "Times", "Liberation Serif" } },
            { "NimbusMono", new[] { "Nimbus Mono", "Courier New", "Courier", "Liberation Mono" } },
            { "SourceSansPro", new[] { "Source Sans Pro", "Helvetica", "Arial", "Liberation Sans" } },
            { "SourceSerifPro", new[] { "Source Serif Pro", "Times New Roman", "Times", "Liberation Serif" } },
            { "SourceCodePro", new[] { "Source Code Pro", "Courier New", "Courier", "Liberation Mono" } },
            { "DroidSans", new[] { "Droid Sans", "Helvetica", "Arial", "Liberation Sans" } },
            { "DroidSerif", new[] { "Droid Serif", "Times New Roman", "Times", "Liberation Serif" } },
            { "DroidSansMono", new[] { "Droid Sans Mono", "Liberation Mono", "Courier New", "Courier" } },
            { "HelveticaNeue", new[] { "Helvetica Neue", "Helvetica", "Arial", "Liberation Sans" } },
            { "ArialUnicodeMS", new[] { "Arial Unicode MS", "Arial", "Helvetica" } },
            { "SegoeUI", new[] { "Segoe UI", "Arial", "Helvetica" } },
            { "SegoeUISymbol", new[] { "Segoe UI Symbol", "Segoe UI", "Arial", "Symbol" } },
            { "SegoeUIEmoji", new[] { "Segoe UI Emoji", "Segoe UI Symbol", "Segoe UI" } }
        };

        /// <summary>
        /// Attempts to match a non-embedded font by normalized stem and style, then falls back to known family substitutions.
        /// </summary>
        /// <param name="baseFont">PDF font base name.</param>
        /// <param name="fontDescriptor">Font descriptor for style hints.</param>
        /// <returns>Matching SKTypeface or SKTypeface.Default if not found.</returns>
        public static SKTypeface SubstituteTypeface(PdfString baseFont, PdfFontDescriptor fontDescriptor)
        {
            var parsed = PdfFontName.Parse(baseFont);
            bool isBold = IsBold(fontDescriptor, parsed);
            bool isItalic = IsItalic(fontDescriptor, parsed);

            SKFontStyle style = SKFontStyle.Normal;
            if (isBold && isItalic)
            {
                style = SKFontStyle.BoldItalic;
            }
            else if (isBold)
            {
                style = SKFontStyle.Bold;
            }
            else if (isItalic)
            {
                style = SKFontStyle.Italic;
            }

            // Stage 1: direct match using normalized stem itself
            var direct = SKFontManager.Default.MatchFamily(parsed.NormalizedStem, style);
            if (direct != null)
            {
                return direct;
            }

            // Stage 2: fallback list
            if (MergedFamilyMap.TryGetValue(parsed.NormalizedStem, out var list))
            {
                for (int i = 0; i < list.Length; i++)
                {
                    var tf = SKFontManager.Default.MatchFamily(list[i], style);
                    if (tf != null)
                    {
                        return tf;
                    }
                }
            }

            return SKTypeface.Default;
        }

        private static bool IsItalic(PdfFontDescriptor fontDescriptor, PdfFontName parsed)
        {
            if (fontDescriptor != null)
            {
                if (fontDescriptor.Flags.HasFlag(PdfFontFlags.Italic))
                {
                    return true;
                }
                if (Math.Abs(fontDescriptor.ItalicAngle) > 0.1f)
                {
                    return true;
                }
            }
            return parsed.ItalicHint;
        }

        private static bool IsBold(PdfFontDescriptor fontDescriptor, PdfFontName parsed)
        {
            if (fontDescriptor != null)
            {
                if (fontDescriptor.Flags.HasFlag(PdfFontFlags.ForceBold))
                {
                    return true;
                }
            }
            return parsed.BoldHint;
        }
    }
}