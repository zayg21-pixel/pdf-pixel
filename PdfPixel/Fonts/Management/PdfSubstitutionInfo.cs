using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

public readonly struct PdfSubstitutionInfo
{
    private static readonly Dictionary<string, SKFontStyleWeight> WeightHints = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Black", SKFontStyleWeight.Black },
        { "Heavy", SKFontStyleWeight.ExtraBold },
        { "ExtraBold", SKFontStyleWeight.ExtraBold },
        { "UltraBold", SKFontStyleWeight.ExtraBold },
        { "Bold", SKFontStyleWeight.Bold },
        { "SemiBold", SKFontStyleWeight.SemiBold },
        { "DemiBold", SKFontStyleWeight.SemiBold },
        { "Medium", SKFontStyleWeight.Medium },
        { "Regular", SKFontStyleWeight.Normal },
        { "Book", SKFontStyleWeight.Normal },
        { "Normal", SKFontStyleWeight.Normal },
        { "Light", SKFontStyleWeight.Light },
        { "ExtraLight", SKFontStyleWeight.ExtraLight },
        { "UltraLight", SKFontStyleWeight.ExtraLight },
        { "Thin", SKFontStyleWeight.Thin }
    };

    private static readonly Dictionary<string, SKFontStyleSlant> SlantHints = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Italic", SKFontStyleSlant.Italic },
        { "Oblique", SKFontStyleSlant.Oblique },
        { "Kursiv", SKFontStyleSlant.Italic },
        { "Slanted", SKFontStyleSlant.Oblique },
        { "Inclined", SKFontStyleSlant.Oblique },
        { "Skewed", SKFontStyleSlant.Oblique },
        { "Cursive", SKFontStyleSlant.Italic }
    };

    private static readonly List<string> StyleHintKeys = CreateStyleHintKeys();

    private static List<string> CreateStyleHintKeys()
    {
        List<string> keys = new(WeightHints.Count + SlantHints.Count);
        keys.AddRange(WeightHints.Keys);
        keys.AddRange(SlantHints.Keys);
        return keys;
    }

    private const float ItalicAngleObliqueMin = 2.0f;
    private const float ItalicAngleItalicMin = 10.0f;

    public string NormalizedStem { get; }

    public SKFontStyle FontStyle { get; }

    public bool IsBold => FontStyle.Weight != 0;

    public bool IsItalic => FontStyle.Slant != SKFontStyleSlant.Upright;

    public PdfSubstitutionInfo()
    {
        NormalizedStem = string.Empty;
        FontStyle = SKFontStyle.Normal;
    }

    public static PdfSubstitutionInfo Detault { get; } = new();

    private PdfSubstitutionInfo(
        string normalizedStem,
        SKFontStyle style)
    {
        NormalizedStem = normalizedStem;
        FontStyle = style;
    }

    public static PdfSubstitutionInfo Parse(in PdfString rawName, PdfFontDescriptor? descriptor)
    {
        if (rawName.IsEmpty)
        {
            return new PdfSubstitutionInfo(string.Empty, SKFontStyle.Normal);
        }

        string name = rawName.ToString();

        int plusIndex = name.IndexOf('+');
        if (plusIndex > 0 && plusIndex < name.Length - 1)
        {
            name = name.Substring(plusIndex + 1);
        }

        if (name.EndsWith("MT", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - 2);
        }

        var weight = SKFontStyleWeight.Normal;
        var slant = SKFontStyleSlant.Upright;
        var width = SKFontStyleWidth.Normal;

        // Single pass over pre-generated keys
        foreach (string key in StyleHintKeys)
        {
            int idx = name.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                if (weight == SKFontStyleWeight.Normal && WeightHints.TryGetValue(key, out SKFontStyleWeight w))
                {
                    weight = w;
                }

                if (slant == SKFontStyleSlant.Upright && SlantHints.TryGetValue(key, out SKFontStyleSlant s))
                {
                    slant = s;
                }

                name = name.Remove(idx, key.Length);
            }
        }

        // Split once after removing hints to compute normalized stem
        string basePart = name;
        int hyphenIndex = name.IndexOf('-');
        if (hyphenIndex > 0)
        {
            basePart = name.Substring(0, hyphenIndex);
        }

        // Descriptor overrides
        if (descriptor != null)
        {
            if ((descriptor.Flags & PdfFontFlags.ForceBold) != 0)
            {
                weight = SKFontStyleWeight.Bold;
            }

            if (descriptor.FontWeight != 0)
            {
                weight = (SKFontStyleWeight)descriptor.FontWeight;
            }

            if ((descriptor.Flags & PdfFontFlags.Italic) != 0)
            {
                slant = SKFontStyleSlant.Italic;
            }

            SKFontStyleSlant angleSlant = GetSlantFromAngle(descriptor.ItalicAngle);
            if (angleSlant != SKFontStyleSlant.Upright)
            {
                slant = angleSlant;
            }

            width = MapWidth(descriptor.FontStretch);
        }

        SKFontStyle style = new(weight, width, slant);
        return new PdfSubstitutionInfo(basePart, style);
    }

    private static SKFontStyleSlant GetSlantFromAngle(float italicAngle)
    {
        float absAngle = Math.Abs(italicAngle);

        if (absAngle >= ItalicAngleItalicMin)
        {
            return SKFontStyleSlant.Italic;
        }

        if (absAngle >= ItalicAngleObliqueMin)
        {
            return SKFontStyleSlant.Oblique;
        }

        return SKFontStyleSlant.Upright;
    }

    private static SKFontStyleWidth MapWidth(in PdfString stretch)
    {
        if (stretch.IsEmpty)
        {
            return SKFontStyleWidth.Normal;
        }

        string value = stretch.ToString();
        if (string.IsNullOrEmpty(value))
        {
            return SKFontStyleWidth.Normal;
        }

        if (Enum.TryParse<SKFontStyleWidth>(value, ignoreCase: true, out SKFontStyleWidth parsedWidth))
        {
            return parsedWidth;
        }

        return SKFontStyleWidth.Normal;
    }
}
