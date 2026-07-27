using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Holds normalized font substitution hints derived from a PDF font's name and font descriptor.
/// Used by the font provider to select a matching typeface when the embedded font is unavailable.
/// </summary>
public readonly struct PdfSubstitutionInfo : IEquatable<PdfSubstitutionInfo>
{
    private static readonly Dictionary<string, int> WeightHints = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Black", 900 },
        { "Heavy", 800 },
        { "ExtraBold", 800 },
        { "UltraBold", 800 },
        { "Bold", 700 },
        { "SemiBold", 600 },
        { "DemiBold", 600 },
        { "Medium", 500 },
        { "Regular", 400 },
        { "Book", 400 },
        { "Normal", 400 },
        { "Light", 300 },
        { "ExtraLight", 200 },
        { "UltraLight", 200 },
        { "Thin", 100 }
    };

    private static readonly HashSet<string> SlantHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "Italic", "Oblique", "Kursiv", "Slanted", "Inclined", "Skewed", "Cursive"
    };

    private static readonly List<string> StyleHintKeys = CreateStyleHintKeys();

    private static List<string> CreateStyleHintKeys()
    {
        List<string> keys = new(WeightHints.Count + SlantHints.Count);
        keys.AddRange(WeightHints.Keys);
        keys.AddRange(SlantHints);
        return keys;
    }

    private const int NormalWeight = 400;
    private const int BoldWeight = 700;
    private const float ItalicAngleObliqueMin = 2.0f;

    /// <summary>
    /// The font family name after stripping style suffixes, subset prefixes (e.g., "ABCDEF+"), and the trailing "MT" suffix.
    /// </summary>
    public string NormalizedStem { get; }

    /// <summary>
    /// <see langword="true"/> when the font name or descriptor indicates a bold weight.
    /// </summary>
    public bool IsBold { get; }

    /// <summary>
    /// <see langword="true"/> when the font name or descriptor indicates an italic or oblique slant.
    /// </summary>
    public bool IsItalic { get; }

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
        IsBold = false;
        IsItalic = false;
    }

    /// <summary>
    /// Initializes a new <see cref="PdfSubstitutionInfo"/> from an explicit normalized stem and style.
    /// </summary>
    /// <param name="normalizedStem">The normalized font family stem.</param>
    /// <param name="isBold">Whether the font is bold.</param>
    /// <param name="isItalic">Whether the font is italic.</param>
    public PdfSubstitutionInfo(string normalizedStem, bool isBold, bool isItalic)
    {
        NormalizedStem = normalizedStem;
        IsBold = isBold;
        IsItalic = isItalic;
    }

    /// <summary>
    /// Initializes a new <see cref="PdfSubstitutionInfo"/> for a Standard 14 font family and style.
    /// </summary>
    /// <param name="standardFontName">The standard font family.</param>
    /// <param name="isBold">Whether the font is bold.</param>
    /// <param name="isItalic">Whether the font is italic.</param>
    public PdfSubstitutionInfo(PdfStandardFontName standardFontName, bool isBold, bool isItalic)
        : this(standardFontName.ToString(), isBold, isItalic)
    {
    }

    /// <summary>
    /// Returns a default <see cref="PdfSubstitutionInfo"/> with an empty stem and normal style.
    /// </summary>
    public static PdfSubstitutionInfo Default { get; } = new();

    /// <summary>
    /// Parses a <see cref="PdfSubstitutionInfo"/> from a raw PDF font name string and an optional font descriptor.
    /// Strips subset prefixes and style tokens from the name, then applies descriptor overrides for weight and slant.
    /// </summary>
    /// <param name="rawName">The raw font name string from the PDF font dictionary (e.g., the /BaseFont value).</param>
    /// <param name="descriptor">Optional font descriptor that may override weight and slant derived from the name.</param>
    /// <returns>A <see cref="PdfSubstitutionInfo"/> containing the normalized stem and resolved style.</returns>
    public static PdfSubstitutionInfo Parse(in PdfString rawName, PdfFontDescriptor? descriptor)
    {
        if (rawName.IsEmpty)
        {
            return new PdfSubstitutionInfo(string.Empty, false, false);
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

        int weight = NormalWeight;
        var isItalic = false;

        // Single pass over pre-generated keys
        foreach (string key in StyleHintKeys)
        {
            int idx = name.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                if (weight == NormalWeight && WeightHints.TryGetValue(key, out int hintWeight))
                {
                    weight = hintWeight;
                }

                if (!isItalic && SlantHints.Contains(key))
                {
                    isItalic = true;
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
                weight = BoldWeight;
            }

            if (descriptor.FontWeight != 0)
            {
                weight = descriptor.FontWeight;
            }

            if ((descriptor.Flags & PdfFontFlags.Italic) != 0)
            {
                isItalic = true;
            }

            if (Math.Abs(descriptor.ItalicAngle) >= ItalicAngleObliqueMin)
            {
                isItalic = true;
            }
        }

        return new PdfSubstitutionInfo(basePart, weight >= BoldWeight, isItalic);
    }

    /// <inheritdoc/>
    public bool Equals(PdfSubstitutionInfo other)
    {
        return string.Equals(NormalizedStem, other.NormalizedStem, StringComparison.OrdinalIgnoreCase)
            && IsBold == other.IsBold
            && IsItalic == other.IsItalic;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfSubstitutionInfo other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(NormalizedStem, IsBold, IsItalic);

    /// <summary>
    /// Determines whether two <see cref="PdfSubstitutionInfo"/> values are equal.
    /// </summary>
    public static bool operator ==(in PdfSubstitutionInfo left, in PdfSubstitutionInfo right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="PdfSubstitutionInfo"/> values are not equal.
    /// </summary>
    public static bool operator !=(in PdfSubstitutionInfo left, in PdfSubstitutionInfo right) => !left.Equals(right);
}
