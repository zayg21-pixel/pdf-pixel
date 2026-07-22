using PdfPixel.Fonts;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using PdfPixel.Models;
using PdfPixel.Resources;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Resolves Standard 14 AFM glyph widths from the embedded width resources, keyed by code 0-255
/// under the font's default encoding.
/// </summary>
internal static class Standard14Metrics
{
    private const int CourierWidth = 600;

    // Every (font name, bold, italic) combination we support, mapped directly to its resource file name.
    private static readonly Dictionary<StandardFontVariantKey, string> VariantFileNames = new()
    {
        [new StandardFontVariantKey(PdfStandardFontName.Times, false, false)] = "TimesRoman",
        [new StandardFontVariantKey(PdfStandardFontName.Times, true, false)] = "TimesBold",
        [new StandardFontVariantKey(PdfStandardFontName.Times, false, true)] = "TimesItalic",
        [new StandardFontVariantKey(PdfStandardFontName.Times, true, true)] = "TimesBoldItalic",

        [new StandardFontVariantKey(PdfStandardFontName.TimesNewRoman, false, false)] = "TimesRoman",
        [new StandardFontVariantKey(PdfStandardFontName.TimesNewRoman, true, false)] = "TimesBold",
        [new StandardFontVariantKey(PdfStandardFontName.TimesNewRoman, false, true)] = "TimesItalic",
        [new StandardFontVariantKey(PdfStandardFontName.TimesNewRoman, true, true)] = "TimesBoldItalic",

        [new StandardFontVariantKey(PdfStandardFontName.TimesNewRomanPS, false, false)] = "TimesRoman",
        [new StandardFontVariantKey(PdfStandardFontName.TimesNewRomanPS, true, false)] = "TimesBold",
        [new StandardFontVariantKey(PdfStandardFontName.TimesNewRomanPS, false, true)] = "TimesItalic",
        [new StandardFontVariantKey(PdfStandardFontName.TimesNewRomanPS, true, true)] = "TimesBoldItalic",

        [new StandardFontVariantKey(PdfStandardFontName.Helvetica, false, false)] = "Helvetica",
        [new StandardFontVariantKey(PdfStandardFontName.Helvetica, true, false)] = "HelveticaBold",
        [new StandardFontVariantKey(PdfStandardFontName.Helvetica, false, true)] = "HelveticaItalic",
        [new StandardFontVariantKey(PdfStandardFontName.Helvetica, true, true)] = "HelveticaBoldItalic",

        [new StandardFontVariantKey(PdfStandardFontName.Arial, false, false)] = "Helvetica",
        [new StandardFontVariantKey(PdfStandardFontName.Arial, true, false)] = "HelveticaBold",
        [new StandardFontVariantKey(PdfStandardFontName.Arial, false, true)] = "HelveticaItalic",
        [new StandardFontVariantKey(PdfStandardFontName.Arial, true, true)] = "HelveticaBoldItalic",

        [new StandardFontVariantKey(PdfStandardFontName.Symbol, false, false)] = "Symbol",
        [new StandardFontVariantKey(PdfStandardFontName.Symbol, true, false)] = "Symbol",
        [new StandardFontVariantKey(PdfStandardFontName.Symbol, false, true)] = "Symbol",
        [new StandardFontVariantKey(PdfStandardFontName.Symbol, true, true)] = "Symbol",

        [new StandardFontVariantKey(PdfStandardFontName.ZapfDingbats, false, false)] = "ZapfDingbats",
        [new StandardFontVariantKey(PdfStandardFontName.ZapfDingbats, true, false)] = "ZapfDingbats",
        [new StandardFontVariantKey(PdfStandardFontName.ZapfDingbats, false, true)] = "ZapfDingbats",
        [new StandardFontVariantKey(PdfStandardFontName.ZapfDingbats, true, true)] = "ZapfDingbats"
    };

    // Resource files loaded so far, keyed by file name. Populated on demand by GetVariantWidths.
    private static readonly Dictionary<string, IReadOnlyDictionary<PdfString, ushort>> VariantWidthsCache = [];

    /// <summary>
    /// Builds the default (code 0-255) AFM width vector for a Standard 14 font, in glyph space units (1/1000 em).
    /// </summary>
    /// <param name="fontName">The Standard 14 font family.</param>
    /// <param name="bold">Whether to resolve the bold style variant.</param>
    /// <param name="italic">Whether to resolve the italic/oblique style variant.</param>
    /// <param name="encoding">
    /// The font's actual encoding (as configured by the PDF's <c>/Encoding</c>, matching what the glyph-ID
    /// resolution path uses). Falls back to the family's default encoding when <see cref="PdfEncoding.Unknown"/>.
    /// </param>
    /// <returns>A 256-entry width vector, or <see langword="null"/> if the family or style variant is unknown.</returns>
    public static int[]? GetWidths(PdfStandardFontName fontName, bool bold, bool italic, PdfEncoding encoding)
    {
        if (encoding == PdfEncoding.Unknown)
        {
            PdfFontEncoding? defaultEncoding = SingleByteEncodings.GetDefaultEncoding(fontName.ToPdfFontStandardName());
            if (defaultEncoding == null)
            {
                return null;
            }

            encoding = defaultEncoding.Value.ToPdfEncoding();
        }

        int? courierWidth = GetCourierWidth(fontName);
        if (courierWidth.HasValue)
        {
            var widths = new int[byte.MaxValue + 1];
            for (int code = 0; code < widths.Length; code++)
            {
                widths[code] = courierWidth.Value;
            }

            return widths;
        }

        IReadOnlyDictionary<PdfString, ushort>? glyphWidths = GetVariantWidths(new StandardFontVariantKey(fontName, bold, italic));
        if (glyphWidths == null)
        {
            return null;
        }

        return BuildWidths(encoding, glyphWidths);
    }

    private static int? GetCourierWidth(PdfStandardFontName fontName)
    {
        return fontName switch
        {
            PdfStandardFontName.Courier or PdfStandardFontName.CourierNew or PdfStandardFontName.CourierNewPS => CourierWidth,
            _ => null
        };
    }

    private static int[] BuildWidths(PdfEncoding encoding, IReadOnlyDictionary<PdfString, ushort> glyphWidths)
    {
        var widths = new int[byte.MaxValue + 1];

        for (int code = 0; code <= byte.MaxValue; code++)
        {
            PdfString name = SingleByteEncodings.GetNameByCode((byte)code, encoding.ToPdfFontEncoding()).ToPdfString();
            if (glyphWidths.TryGetValue(name, out ushort width))
            {
                widths[code] = width;
            }
        }

        return widths;
    }

    private static IReadOnlyDictionary<PdfString, ushort>? GetVariantWidths(in StandardFontVariantKey key)
    {
        if (!VariantFileNames.TryGetValue(key, out string? variantFileName))
        {
            return null;
        }

        if (!VariantWidthsCache.TryGetValue(variantFileName, out IReadOnlyDictionary<PdfString, ushort>? glyphWidths))
        {
            byte[] blob = PdfResourceLoader.GetResource($"Metrics.{variantFileName}Width.bin");
            glyphWidths = PdfTextResourceConverter.FromWidthMapBlob(blob);
            VariantWidthsCache[variantFileName] = glyphWidths;
        }

        return glyphWidths;
    }

    private readonly struct StandardFontVariantKey
    {
        public StandardFontVariantKey(PdfStandardFontName fontName, bool bold, bool italic)
        {
            FontName = fontName;
            Bold = bold;
            Italic = italic;
        }

        public PdfStandardFontName FontName { get; }

        public bool Bold { get; }

        public bool Italic { get; }
    }
}
