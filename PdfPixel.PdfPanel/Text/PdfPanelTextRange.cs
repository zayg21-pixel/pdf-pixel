using System;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// A run of consecutive characters on a page, as indexes into the page's extracted characters.
/// Ranges are ordered by page, then by start index, then by length.
/// </summary>
public readonly struct PdfPanelTextRange : IComparable<PdfPanelTextRange>, IEquatable<PdfPanelTextRange>
{
    /// <summary>
    /// Initializes a range of <paramref name="length"/> characters starting at <paramref name="startIndex"/>
    /// on the given 1-based page number.
    /// </summary>
    public PdfPanelTextRange(int pageNumber, int startIndex, int length)
    {
        PageNumber = pageNumber;
        StartIndex = startIndex;
        Length = length;
    }

    /// <summary>
    /// 1-based page number the range is on.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Index of the first character of the range in the page's characters.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// Number of characters in the range.
    /// </summary>
    public int Length { get; }

    /// <inheritdoc />
    public int CompareTo(PdfPanelTextRange other)
    {
        if (PageNumber != other.PageNumber)
        {
            return PageNumber.CompareTo(other.PageNumber);
        }

        if (StartIndex != other.StartIndex)
        {
            return StartIndex.CompareTo(other.StartIndex);
        }

        return Length.CompareTo(other.Length);
    }

    /// <inheritdoc />
    public bool Equals(PdfPanelTextRange other)
    {
        return PageNumber == other.PageNumber
            && StartIndex == other.StartIndex
            && Length == other.Length;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PdfPanelTextRange other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(PageNumber, StartIndex, Length);

    /// <summary>
    /// Returns whether two ranges cover the same characters.
    /// </summary>
    public static bool operator ==(in PdfPanelTextRange left, in PdfPanelTextRange right) => left.Equals(right);

    /// <summary>
    /// Returns whether two ranges cover different characters.
    /// </summary>
    public static bool operator !=(in PdfPanelTextRange left, in PdfPanelTextRange right) => !left.Equals(right);

    /// <summary>
    /// Returns whether <paramref name="left"/> is ordered before <paramref name="right"/>.
    /// </summary>
    public static bool operator <(in PdfPanelTextRange left, in PdfPanelTextRange right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Returns whether <paramref name="left"/> is ordered before or equal to <paramref name="right"/>.
    /// </summary>
    public static bool operator <=(in PdfPanelTextRange left, in PdfPanelTextRange right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Returns whether <paramref name="left"/> is ordered after <paramref name="right"/>.
    /// </summary>
    public static bool operator >(in PdfPanelTextRange left, in PdfPanelTextRange right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Returns whether <paramref name="left"/> is ordered after or equal to <paramref name="right"/>.
    /// </summary>
    public static bool operator >=(in PdfPanelTextRange left, in PdfPanelTextRange right) => left.CompareTo(right) >= 0;
}
