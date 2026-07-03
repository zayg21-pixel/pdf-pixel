using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Holds normalized font substitution hints derived from a PDF font's name and font descriptor.
/// Used by the font substitutor to select a matching system typeface when the embedded font is unavailable.
/// </summary>
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

    /// <summary>
    /// The font family name after stripping style suffixes, subset prefixes (e.g., "ABCDEF+"), and the trailing "MT" suffix.
    /// </summary>
    public string NormalizedStem { get; }

    /// <summary>
    /// The resolved Skia font style (weight, width, slant) inferred from the font name and font descriptor.
    /// </summary>
    public SKFontStyle FontStyle { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="FontStyle"/> indicates a weight heavier than normal.
    /// </summary>
    public bool IsBold => FontStyle.Weight >= (int)SKFontStyleWeight.Bold;

    /// <summary>
    /// <see langword="true"/> when <see cref="FontStyle"/> specifies an italic or oblique slant.
    /// </summary>
    public bool IsItalic => FontStyle.Slant != SKFontStyleSlant.Upright;

    /// <summary>
    /// Resolves <see cref="NormalizedStem"/> to a Standard 14 font family, or <see langword="null"/> if it isn't one.
    /// </summary>
    public PdfStandardFontName? GetStandardName() => (Enum.TryParse(NormalizedStem, out PdfStandardFontName standardFontName)) ? standardFontName : null;

    /// <summary>
    /// Initializes a new <see cref="PdfSubstitutionInfo"/> with an empty stem and normal style.
    /// </summary>
    public PdfSubstitutionInfo()
    {
        NormalizedStem = string.Empty;
        FontStyle = SKFontStyle.Normal;
    }

    /// <summary>
    /// Returns a default <see cref="PdfSubstitutionInfo"/> with an empty stem and normal style.
    /// </summary>
    public static PdfSubstitutionInfo Detault { get; } = new();

    private PdfSubstitutionInfo(
        string normalizedStem,
        SKFontStyle style)
    {
        NormalizedStem = normalizedStem;
        FontStyle = style;
    }

    /// <summary>
    /// Parses a <see cref="PdfSubstitutionInfo"/> from a raw PDF font name string and an optional font descriptor.
    /// Strips subset prefixes and style tokens from the name, then applies descriptor overrides for weight, slant, and width.
    /// </summary>
    /// <param name="rawName">The raw font name string from the PDF font dictionary (e.g., the /BaseFont value).</param>
    /// <param name="descriptor">Optional font descriptor that may override weight, slant, and width derived from the name.</param>
    /// <returns>A <see cref="PdfSubstitutionInfo"/> containing the normalized stem and resolved font style.</returns>
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
        int endIndex = name.Length;
        while (endIndex > 0 && char.IsPunctuation(name[endIndex - 1]))
        {
            endIndex--;
        }

        string basePart = name.Substring(0, endIndex);
        int hyphenIndex = basePart.IndexOf('-');
        if (hyphenIndex > 0)
        {
            basePart = basePart.Substring(0, hyphenIndex);
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
