using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Utility class for working with PDF fonts and text.
    /// Two-stage substitution:
    /// 1. Try direct match of normalized stem.
    /// 2. Fallback sequence from MergedFamilyMap.
    /// </summary>
    public static class PdfFontUtilities
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
            { "DroidSansMono", new[] { "Droid Sans Mono", "Courier New", "Courier", "Liberation Mono" } },
            { "HelveticaNeue", new[] { "Helvetica Neue", "Helvetica", "Arial", "Liberation Sans" } },
            { "ArialUnicodeMS", new[] { "Arial Unicode MS", "Arial", "Helvetica" } },
            { "SegoeUI", new[] { "Segoe UI", "Arial", "Helvetica" } },
            { "SegoeUISymbol", new[] { "Segoe UI Symbol", "Segoe UI", "Arial", "Symbol" } },
            { "SegoeUIEmoji", new[] { "Segoe UI Emoji", "Segoe UI Symbol", "Segoe UI" } }
        };

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
                    Console.WriteLine("Warning: Type3 font " + type3Font.BaseFont + " cannot be converted to SKTypeface");
                    return GetTypefaceFromParametersOrDefault(font);
                default:
                    Console.WriteLine("Warning: Unknown font type " + font.GetType().Name);
                    return GetTypefaceFromParametersOrDefault(font);
            }
        }

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
            return GetTypefaceFromParametersOrDefault(simpleFont);
        }

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
            return GetTypefaceFromParametersOrDefault(cidFont);
        }

        private unsafe static SKTypeface LoadSkiaTypeface(FontFileFormat? fileFormat, PdfFontBase baseFont)
        {
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

        private static SKTypeface GetTypefaceForCompositeFont(PdfCompositeFont compositeFont)
        {
            var primaryDescendant = compositeFont.PrimaryDescendant;
            if (primaryDescendant != null)
            {
                return GetTypefaceForCIDFont(primaryDescendant);
            }
            Console.WriteLine("Warning: Composite font " + compositeFont.BaseFont + " has no descendant fonts");
            return GetTypefaceFromParametersOrDefault(compositeFont);
        }

        private static SKTypeface GetTypefaceFromParametersOrDefault(PdfFontBase font)
        {
            if (font == null)
            {
                return SKTypeface.Default;
            }

            var parsed = PdfFontName.Parse(font.BaseFont);

            bool isBold = IsBold(font, parsed);
            bool isItalic = IsItalic(font, parsed);

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
            foreach (var candidate in EnumerateFallbackCandidates(parsed))
            {
                var tf = SKFontManager.Default.MatchFamily(candidate, style);
                if (tf != null)
                {
                    if (!candidate.Equals(parsed.NormalizedStem, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Info: Substituting font family '" + parsed.NormalizedStem + "' with '" + candidate + "'");
                    }
                    return tf;
                }
            }

            Console.WriteLine("Warning: No matching typeface found for '" + parsed.NormalizedStem + "', attempting cache fallback");
            var cacheFallback = font.Document.FontCache.GetFallbackFromParameters(style.Width, style.Weight, style.Slant);
            if (cacheFallback != null)
            {
                return cacheFallback;
            }
            Console.WriteLine("Warning: Cache fallback unavailable, using SKTypeface.Default");
            return SKTypeface.Default;
        }

        private static IEnumerable<string> EnumerateFallbackCandidates(PdfFontName parsed)
        {
            if (MergedFamilyMap.TryGetValue(parsed.NormalizedStem, out var list))
            {
                for (int i = 0; i < list.Length; i++)
                {
                    yield return list[i];
                }
            }
        }

        private static bool IsItalic(PdfFontBase font, PdfFontName parsed)
        {
            if (font?.FontDescriptor != null)
            {
                if (font.FontDescriptor.Flags.HasFlag(PdfFontFlags.Italic))
                {
                    return true;
                }
                if (Math.Abs(font.FontDescriptor.ItalicAngle) > 0.1f)
                {
                    return true;
                }
            }
            return parsed.ItalicHint;
        }

        private static bool IsBold(PdfFontBase font, PdfFontName parsed)
        {
            if (font?.FontDescriptor != null)
            {
                if (font.FontDescriptor.Flags.HasFlag(PdfFontFlags.ForceBold))
                {
                    return true;
                }
            }
            return parsed.BoldHint;
        }
    }
}