using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Utility class for working with PDF fonts and text
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    public static class PdfFontUtilities
    {
        // Static map of Standard 14 and common PostScript family names to candidate substitutions.
        // The first entry should be the best match on most platforms.
        private static readonly Dictionary<string, string[]> StandardFamilyFallbacks = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Helvetica", new[] { "Helvetica", "Arial", "Liberation Sans", "Nimbus Sans" } },
            { "Times", new[] { "Times New Roman", "Times", "Liberation Serif", "Nimbus Roman" } },
            { "Times-Roman", new[] { "Times New Roman", "Times", "Liberation Serif", "Nimbus Roman" } },
            { "Courier", new[] { "Courier New", "Courier", "Liberation Mono", "Nimbus Mono" } },
            { "Symbol", new[] { "Symbol", "Segoe UI Symbol" } },
            { "ZapfDingbats", new[] { "Segoe UI Symbol", "Wingdings", "Zapf Dingbats" } }
        };

        // Static style suffix list to strip style hints from BaseFont when deriving family name
        private static readonly string[] StyleSuffixes = new[]
        {
            "-Bold", "-Italic", "-BoldItalic", "-Oblique", "-BoldOblique", ",Bold", ",Italic"
        };

        /// <summary>
        /// Retrieves a SkiaSharp <see cref="SKTypeface"/> instance based on the specified PDF font.
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public static SKTypeface GetTypeface(PdfFontBase font)
        {
            if (font == null)
            {
                return SKTypeface.Default;
            }

            switch (font)
            {
                case PdfSimpleFont simpleFont:
                    return GetTypefaceForSimpleFont(simpleFont);

                case PdfCIDFont cidFont:
                    return GetTypefaceForCIDFont(cidFont);

                case PdfCompositeFont compositeFont:
                    return GetTypefaceForCompositeFont(compositeFont);

                case PdfType3Font type3Font:
                    // Type3 fonts don't have embedded font files
                    Console.WriteLine($"Warning: Type3 font {type3Font.BaseFont} cannot be converted to SKTypeface");
                    return GetTypeFaceFromParametersOrDefault(font);

                default:
                    Console.WriteLine($"Warning: Unknown font type {font.GetType().Name}");
                    return GetTypeFaceFromParametersOrDefault(font);
            }
        }

        /// <summary>
        /// Get typeface for simple fonts
        /// </summary>
        private static SKTypeface GetTypefaceForSimpleFont(PdfSimpleFont simpleFont)
        {
            if (simpleFont.IsEmbedded)
            {
                var result = LoadSkiaTypeface(simpleFont.FontDescriptor?.FontFileFormat, simpleFont);
                if (result != null)
                {
                    return result;
                }
            }

            return GetTypeFaceFromParametersOrDefault(simpleFont);
        }

        /// <summary>
        /// Get typeface for CID fonts
        /// </summary>
        private static SKTypeface GetTypefaceForCIDFont(PdfCIDFont cidFont)
        {
            if (cidFont.IsEmbedded)
            {
                var result = LoadSkiaTypeface(cidFont.FontDescriptor?.FontFileFormat, cidFont);
                if (result != null)
                {
                    return result;
                }
            }

            return GetTypeFaceFromParametersOrDefault(cidFont);
        }

        private unsafe static SKTypeface LoadSkiaTypeface(FontFileFormat? fileFormat, PdfFontBase baseFont)
        {
            // For TrueType/OpenType or when wrapping failed, let SkiaSharp load from the raw font bytes
            var memory = baseFont.FontDescriptor?.GetFontStream() ?? default;
            var handle = memory.Pin();
            bool created = false;
            try
            {
                IntPtr addr = (IntPtr)handle.Pointer;
                SKDataReleaseDelegate release = (address, ctx) =>
                {
                    if (ctx is IDisposable disp)
                    {
                        disp.Dispose();
                    }
                };

                using var data = SKData.Create(addr, memory.Length, release, handle);
                var typeface = SKTypeface.FromData(data);
                created = true;
                return typeface;
            }
            finally
            {
                if (!created)
                {
                    handle.Dispose();
                }
            }
        }

        /// <summary>
        /// Get typeface for composite fonts (delegates to primary descendant)
        /// </summary>
        private static SKTypeface GetTypefaceForCompositeFont(PdfCompositeFont compositeFont)
        {
            var primaryDescendant = compositeFont.PrimaryDescendant;
            if (primaryDescendant != null)
            {
                return GetTypefaceForCIDFont(primaryDescendant);
            }

            Console.WriteLine($"Warning: Composite font {compositeFont.BaseFont} has no descendant fonts");
            return GetTypeFaceFromParametersOrDefault(compositeFont);
        }

        /// <summary>
        /// Retrieves an <see cref="SKTypeface"/> based on the specified <paramref name="font"/> parameters
        /// or <see cref="SKTypeface.Default"/>.
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public static SKTypeface GetTypeFaceFromParametersOrDefault(PdfFontBase font)
        {
            if (font == null)
            {
                return SKTypeface.Default;
            }

            // Map to system fonts as final fallback
            var fontFamily = GetFontFamily(font);
            var isBold = IsBold(font);
            var isItalic = IsItalic(font);

            var style = SKFontStyle.Normal;
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

            // Try candidates from static fallback dictionary
            foreach (var candidate in GetCandidateFamilies(fontFamily))
            {
                var tf = SKFontManager.Default.MatchFamily(candidate, style);
                if (tf != null)
                {
                    if (!string.Equals(candidate, fontFamily, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Info: Substituting font family '{fontFamily}' with '{candidate}'");
                    }
                    return tf;
                }
            }

            // Final fallback to configured cache
            var fallback = font.Document.FontCache.GetFallbackFromParameters(style.Width, style.Weight, style.Slant);
            if (fallback == null)
            {
                Console.WriteLine($"Warning: No matching typeface found for '{fontFamily}', using SKTypeface.Default");
                return SKTypeface.Default;
            }

            return fallback;
        }

        private static IEnumerable<string> GetCandidateFamilies(string fontFamily)
        {
            // Always try the exact family name first
            if (!string.IsNullOrEmpty(fontFamily))
            {
                yield return fontFamily;
            }

            if (string.IsNullOrEmpty(fontFamily))
            {
                yield break;
            }

            if (StandardFamilyFallbacks.TryGetValue(fontFamily, out var mappedList))
            {
                for (int i = 0; i < mappedList.Length; i++)
                {
                    var candidate = mappedList[i];
                    if (!string.Equals(candidate, fontFamily, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a font has italic characteristics
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public static bool IsItalic(PdfFontBase font)
        {
            if (font?.FontDescriptor != null)
            {
                return font.FontDescriptor.Flags.HasFlag(PdfFontFlags.Italic) ||
                       Math.Abs(font.FontDescriptor.ItalicAngle) > 0.1f;
            }

            // Fallback: check font name
            var fontName = font?.BaseFont?.ToLowerInvariant() ?? string.Empty;
            return fontName.Contains("italic") || fontName.Contains("oblique");
        }

        /// <summary>
        /// Checks if a font has bold characteristics
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public static bool IsBold(PdfFontBase font)
        {
            if (font?.FontDescriptor != null)
            {
                return font.FontDescriptor.Flags.HasFlag(PdfFontFlags.ForceBold);
            }

            // Fallback: check font name
            var fontName = font?.BaseFont?.ToLowerInvariant() ?? string.Empty;
            return fontName.Contains("bold");
        }

        /// <summary>
        /// Gets the font family name from the base font name
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public static string GetFontFamily(PdfFontBase font)
        {
            var baseFont = font?.BaseFont;
            if (string.IsNullOrEmpty(baseFont))
            {
                return "Unknown";
            }

            // Remove common suffixes
            var familyName = baseFont;

            // Handle embedded font names with subset prefixes (e.g., "ABCDEF+Arial")
            var plusIndex = familyName.IndexOf('+');
            if (plusIndex >= 0 && plusIndex < familyName.Length - 1)
            {
                familyName = familyName.Substring(plusIndex + 1);
            }

            // Remove style suffixes using the static list
            for (int i = 0; i < StyleSuffixes.Length; i++)
            {
                var suffix = StyleSuffixes[i];
                if (familyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    familyName = familyName.Substring(0, familyName.Length - suffix.Length);
                    break;
                }
            }

            return familyName;
        }
    }
}