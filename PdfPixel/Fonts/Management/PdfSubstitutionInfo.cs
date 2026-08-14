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
    /// <summary>
    /// The CSS/OpenType weight class (100-1000) corresponding to normal-weight text.
    /// </summary>
    public const int NormalWeight = 400;

    /// <summary>
    /// The CSS/OpenType weight class (100-1000) at and above which text is considered bold.
    /// </summary>
    public const int BoldWeight = 700;

    /// <summary>
    /// The CSS/OpenType width (stretch) class (1-9, <c>usWidthClass</c>) corresponding to normal-width text.
    /// </summary>
    public const int NormalWidth = 5;

    /// <summary>
    /// The italic angle (in degrees, standard sign convention - see <see cref="ItalicAngle"/>) used for a font
    /// detected as italic by name or by the <see cref="PdfFontFlags.Italic"/> descriptor flag, when no explicit
    /// non-zero <see cref="PdfFontDescriptor.ItalicAngle"/> is available to use instead. Representative of a
    /// typical italic/oblique slant, not measured.
    /// </summary>
    private const float NominalItalicAngle = 12.0f;

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

    // Keys ordered so a longer hint (e.g. "SemiCondensed") is matched before a shorter one it contains
    // (e.g. "Condensed") - the single-pass loop in ExtractNameHints removes each match from the name as it
    // goes, so matching the longer hint first is what keeps the shorter one from also firing.
    private static readonly Dictionary<string, int> WidthHints = new(StringComparer.OrdinalIgnoreCase)
    {
        { "UltraCondensed", 1 },
        { "ExtraCondensed", 2 },
        { "SemiCondensed", 4 },
        { "Condensed", 3 },
        { "Narrow", 3 },
        { "SemiExpanded", 6 },
        { "ExtraExpanded", 8 },
        { "UltraExpanded", 9 },
        { "Expanded", 7 },
        { "Wide", 7 },
        { "Normal", 5 }
    };

    private static readonly HashSet<string> SlantHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "Italic", "Oblique", "Kursiv", "Slanted", "Inclined", "Skewed", "Cursive"
    };

    private static readonly List<string> StyleHintKeys = CreateStyleHintKeys();

    /// <summary>
    /// Initializes a new <see cref="PdfSubstitutionInfo"/> with an empty stem and normal style.
    /// </summary>
    public PdfSubstitutionInfo()
    {
        NormalizedStem = string.Empty;
        Weight = NormalWeight;
        Width = NormalWidth;
        ItalicAngle = 0f;
    }

    /// <summary>
    /// Initializes a new <see cref="PdfSubstitutionInfo"/> from an explicit normalized stem and style.
    /// </summary>
    /// <param name="normalizedStem">The normalized font family stem.</param>
    /// <param name="weight">The font's weight, on the CSS/OpenType weight class scale (100-1000).</param>
    /// <param name="width">The font's width (stretch), on the CSS/OpenType <c>usWidthClass</c> scale (1-9).</param>
    /// <param name="italicAngle">The font's italic angle in degrees, or zero when upright.</param>
    public PdfSubstitutionInfo(string normalizedStem, int weight, int width, float italicAngle)
    {
        NormalizedStem = normalizedStem;
        Weight = weight;
        Width = width;
        ItalicAngle = italicAngle;
    }

    /// <summary>
    /// Initializes a new <see cref="PdfSubstitutionInfo"/> for a Standard 14 font family and style.
    /// </summary>
    /// <param name="standardFontName">The standard font family.</param>
    /// <param name="weight">The font's weight, on the CSS/OpenType weight class scale (100-1000).</param>
    /// <param name="width">The font's width (stretch), on the CSS/OpenType <c>usWidthClass</c> scale (1-9).</param>
    /// <param name="italicAngle">The font's italic angle in degrees, or zero when upright.</param>
    public PdfSubstitutionInfo(PdfStandardFontName standardFontName, int weight, int width, float italicAngle)
        : this(standardFontName.ToString(), weight, width, italicAngle)
    {
    }

    /// <summary>
    /// The font family name after stripping style suffixes, subset prefixes (e.g., "ABCDEF+"), and the trailing "MT" suffix.
    /// </summary>
    public string NormalizedStem { get; }

    /// <summary>
    /// The font's weight, on the CSS/OpenType weight class scale (100-1000), derived from the font name or descriptor.
    /// </summary>
    public int Weight { get; }

    /// <summary>
    /// The font's width (stretch), on the CSS/OpenType <c>usWidthClass</c> scale (1-9, Ultra-condensed to Ultra-expanded),
    /// derived from the font name or descriptor.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// The font's italic angle in degrees, derived from the font name or descriptor. Zero when the font is upright,
    /// positive for the common rightward-leaning slant. This is the opposite sign from the PDF spec's
    /// <see cref="PdfFontDescriptor.ItalicAngle"/>, which is negative for that same slant direction.
    /// </summary>
    public float ItalicAngle { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="ItalicAngle"/> is non-zero.
    /// </summary>
    public bool IsItalic => ItalicAngle != 0f;

    /// <summary>
    /// Returns a default <see cref="PdfSubstitutionInfo"/> with an empty stem and normal style.
    /// </summary>
    public static PdfSubstitutionInfo Default { get; } = new();

    /// <summary>
    /// Resolves <see cref="NormalizedStem"/> to a Standard 14 font family, or <see langword="null"/> if it isn't one.
    /// </summary>
    public PdfStandardFontName? GetStandardName() => (Enum.TryParse(NormalizedStem, out PdfStandardFontName standardFontName)) ? standardFontName : null;

    /// <summary>
    /// Parses a <see cref="PdfSubstitutionInfo"/> from a raw PDF font name string and an optional font descriptor.
    /// Strips subset prefixes and style tokens from the name to compute the normalized stem. When a descriptor
    /// is present, weight, width, and slant are resolved from its literal fields, falling back to its font name -
    /// the raw font name is never consulted in that case. Only when no descriptor is present is the raw font
    /// name used to derive weight, width, and slant.
    /// </summary>
    /// <param name="rawName">The raw font name string from the PDF font dictionary (e.g., the /BaseFont value).</param>
    /// <param name="descriptor">Optional font descriptor that may override weight, width, and slant derived from the name.</param>
    /// <returns>A <see cref="PdfSubstitutionInfo"/> containing the normalized stem and resolved style.</returns>
    public static PdfSubstitutionInfo Parse(PdfString? rawName, PdfFontDescriptor? descriptor)
    {
        string name = (rawName == null) ? string.Empty : rawName.Value.ToString();

        int plusIndex = name.IndexOf('+');
        if (plusIndex > 0 && plusIndex < name.Length - 1)
        {
            name = name.Substring(plusIndex + 1);
        }

        if (name.EndsWith("MT", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - 2);
        }

        FontStyleHints rawNameHints = ExtractNameHints(ref name);

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

        FontStyleHints resolved;
        if (descriptor != null)
        {
            FontStyleHints literalHints = GetLiteralDescriptorHints(descriptor);

            string descriptorFontName = (descriptor.FontName == null) ? string.Empty : descriptor.FontName.Value.ToString();
            FontStyleHints descriptorNameHints = ExtractNameHints(ref descriptorFontName);

            resolved = literalHints.Coalesce(descriptorNameHints);
        }
        else
        {
            resolved = rawNameHints;
        }

        int weight = resolved.Weight ?? NormalWeight;
        int width = resolved.Width ?? NormalWidth;
        float italicAngle = resolved.ItalicAngle ?? 0f;

        return new PdfSubstitutionInfo(basePart, weight, width, italicAngle);
    }

    private static List<string> CreateStyleHintKeys()
    {
        List<string> keys = new(WeightHints.Count + WidthHints.Count + SlantHints.Count);
        keys.AddRange(WeightHints.Keys);
        keys.AddRange(WidthHints.Keys);
        keys.AddRange(SlantHints);
        return keys;
    }

    /// <summary>
    /// Derives weight/width/slant hints from the descriptor's own literal fields (/Flags, /FontWeight,
    /// /FontStretch, /ItalicAngle). /ItalicAngle is a required descriptor entry, so a non-zero value is
    /// always trusted directly; a zero value falls back to the <see cref="PdfFontFlags.Italic"/> flag
    /// before name-based hints are consulted.
    /// </summary>
    private static FontStyleHints GetLiteralDescriptorHints(PdfFontDescriptor descriptor)
    {
        int? weight = ((descriptor.Flags & PdfFontFlags.ForceBold) != 0) ? BoldWeight : null;
        if (descriptor.FontWeight != null)
        {
            weight = descriptor.FontWeight;
        }

        int? width = (descriptor.FontStretch != null && WidthHints.TryGetValue(descriptor.FontStretch.Value.ToString(), out int descriptorWidth))
            ? descriptorWidth
            : null;

        float? italicAngle = ((descriptor.Flags & PdfFontFlags.Italic) != 0) ? NominalItalicAngle : null;
        if (descriptor.ItalicAngle != 0f)
        {
            // PDF's /ItalicAngle is negative for a rightward-leaning slant; flip to the standard sign convention.
            italicAngle = -descriptor.ItalicAngle;
        }

        return new FontStyleHints(weight, width, italicAngle);
    }

    /// <summary>
    /// Scans <paramref name="name"/> for weight, width, and slant hint tokens, removing each matched
    /// token from <paramref name="name"/> as it is found.
    /// </summary>
    private static FontStyleHints ExtractNameHints(ref string name)
    {
        int? weight = null;
        int? width = null;
        float? italicAngle = null;

        foreach (string key in StyleHintKeys)
        {
            int idx = name.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                if (weight == null && WeightHints.TryGetValue(key, out int hintWeight))
                {
                    weight = hintWeight;
                }

                if (width == null && WidthHints.TryGetValue(key, out int hintWidth))
                {
                    width = hintWidth;
                }

                if (italicAngle == null && SlantHints.Contains(key))
                {
                    italicAngle = NominalItalicAngle;
                }

                name = name.Remove(idx, key.Length);
            }
        }

        return new FontStyleHints(weight, width, italicAngle);
    }

    /// <inheritdoc/>
    public bool Equals(PdfSubstitutionInfo other)
    {
        return string.Equals(NormalizedStem, other.NormalizedStem, StringComparison.OrdinalIgnoreCase)
            && Weight == other.Weight
            && Width == other.Width
            && ItalicAngle == other.ItalicAngle;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfSubstitutionInfo other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(NormalizedStem, Weight, Width, ItalicAngle);

    /// <summary>
    /// Determines whether two <see cref="PdfSubstitutionInfo"/> values are equal.
    /// </summary>
    public static bool operator ==(in PdfSubstitutionInfo left, in PdfSubstitutionInfo right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="PdfSubstitutionInfo"/> values are not equal.
    /// </summary>
    public static bool operator !=(in PdfSubstitutionInfo left, in PdfSubstitutionInfo right) => !left.Equals(right);

    /// <summary>
    /// Nullable weight/width/slant hints from a single source (the descriptor's literal fields, the
    /// descriptor's font name, or the raw font name), used to build the source-priority fallback chain
    /// in <see cref="Parse"/>.
    /// </summary>
    private readonly struct FontStyleHints
    {
        public int? Weight { get; }

        public int? Width { get; }

        public float? ItalicAngle { get; }

        public FontStyleHints(int? weight, int? width, float? italicAngle)
        {
            Weight = weight;
            Width = width;
            ItalicAngle = italicAngle;
        }

        /// <summary>
        /// Fills any field left <see langword="null"/> in this instance from <paramref name="fallback"/>.
        /// </summary>
        public FontStyleHints Coalesce(in FontStyleHints fallback) => new(Weight ?? fallback.Weight, Width ?? fallback.Width, ItalicAngle ?? fallback.ItalicAngle);
    }
}
