using PdfReader.Fonts.Types;
using PdfReader.Models;
using System;

namespace PdfReader.Fonts.Management;

/// <summary>
/// Parsed representation of a PDF BaseFont PostScript name.
/// Performs syntactic normalization (subset removal, manufacturer suffix removal, style token detection)
/// and exposes a normalized stem (family stem without style tokens or trailing MT).
/// </summary>
public readonly struct PdfFontName : IEquatable<PdfFontName>
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
    /// Stem after removing subset tag, trailing manufacturer token (MT), and style tokens.
    /// </summary>
    public string NormalizedStem { get; }

    /// <summary>
    /// Style hint parsed from PostScript name tokens.
    /// </summary>
    public bool BoldHint { get; }

    /// <summary>
    /// Style hint parsed from PostScript name tokens.
    /// </summary>
    public bool ItalicHint { get; }

    public PdfFontName(
        string normalizedStem,
        bool boldHint,
        bool italicHint)
    {
        NormalizedStem = normalizedStem;
        BoldHint = boldHint;
        ItalicHint = italicHint;
    }

    /// <summary>
    /// Parse a raw PDF BaseFont name into a <see cref="PdfFontName"/>.
    /// </summary>
    /// <param name="rawName">Raw BaseFont string (may be null or empty).</param>
    /// <param name="descriptor">Font descriptor providing additional font metadata (can be null).</param>
    public static PdfFontName Parse(PdfString rawName, PdfFontDescriptor descriptor)
    {
        if (rawName.IsEmpty)
        {
            return new PdfFontName(string.Empty, false, false);
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

        if (descriptor != null)
        {
            bold = descriptor.Flags.HasFlag(PdfFontFlags.ForceBold);
            italic = descriptor.Flags.HasFlag(PdfFontFlags.Italic) || Math.Abs(descriptor.ItalicAngle) > 0.1f;
        }
        else
        {
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
            else
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
        }

        return new PdfFontName(basePart, bold, italic);
    }

    /// <summary>
    /// Determines whether the specified <see cref="PdfFontName"/> is equal to the current <see cref="PdfFontName"/>.
    /// Comparison is based on NormalizedStem, BoldHint, and ItalicHint.
    /// </summary>
    public bool Equals(PdfFontName other)
    {
        return string.Equals(NormalizedStem, other.NormalizedStem, StringComparison.Ordinal)
            && BoldHint == other.BoldHint
            && ItalicHint == other.ItalicHint;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current <see cref="PdfFontName"/>.
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is PdfFontName other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code for the current <see cref="PdfFontName"/>.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(NormalizedStem, BoldHint, ItalicHint);
    }
}
