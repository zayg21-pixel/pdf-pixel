using System;
using System.Globalization;

namespace PdfPixel.Models;

/// <summary>
/// PDF version number, as declared by the file header or the catalog /Version entry.
/// </summary>
public readonly struct PdfVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfVersion"/> struct.
    /// </summary>
    /// <param name="major">Major version number.</param>
    /// <param name="minor">Minor version number.</param>
    public PdfVersion(int major, int minor)
    {
        Major = major;
        Minor = minor;
    }

    /// <summary>
    /// Major version number.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Minor version number.
    /// </summary>
    public int Minor { get; }

    /// <inheritdoc />
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}.{1}", Major, Minor);

    /// <summary>
    /// Returns whether this version is later than <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The version to compare with.</param>
    internal bool IsLaterThan(in PdfVersion other)
    {
        if (Major != other.Major)
        {
            return Major > other.Major;
        }

        return Minor > other.Minor;
    }

    /// <summary>
    /// Parses a version written as <c>major.minor</c> at the start of <paramref name="text"/>,
    /// or returns <see langword="null"/> when it does not start with one.
    /// </summary>
    /// <param name="text">Bytes holding the version, e.g. the bytes after <c>%PDF-</c> or a /Version name.</param>
    internal static PdfVersion? Parse(in ReadOnlySpan<byte> text)
    {
        int position = 0;
        int? major = ReadNumber(text, ref position);
        if (major == null || position >= text.Length || text[position] != (byte)'.')
        {
            return null;
        }

        position++;
        int? minor = ReadNumber(text, ref position);
        if (minor == null)
        {
            return null;
        }

        return new PdfVersion(major.Value, minor.Value);
    }

    private static int? ReadNumber(in ReadOnlySpan<byte> text, ref int position)
    {
        int start = position;
        int value = 0;

        while (position < text.Length && text[position] >= (byte)'0' && text[position] <= (byte)'9' && position - start < 4)
        {
            value = (value * 10) + (text[position] - (byte)'0');
            position++;
        }

        if (position == start)
        {
            return null;
        }

        return value;
    }
}
