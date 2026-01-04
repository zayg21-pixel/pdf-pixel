using System.Collections.Generic;

namespace PdfReader.Fonts.Mapping;

/// <summary>
/// Comparer for <see cref="UnicodeRangeMap"/> that orders ranges first by length,
/// then by start code value, and finally by end code value. This ordering enables
/// binary search by code value when ranges are kept sorted.
/// </summary>
internal sealed class UnicodeRangeComparer : IComparer<UnicodeRangeMap>
{
    /// <summary>
    /// Singleton instance to avoid allocations.
    /// </summary>
    public static readonly UnicodeRangeComparer Instance = new UnicodeRangeComparer();

    private UnicodeRangeComparer()
    {
    }

    public int Compare(UnicodeRangeMap x, UnicodeRangeMap y)
    {
        if (x.Length != y.Length)
        {
            return x.Length.CompareTo(y.Length);
        }

        int startComparison = x.Start.CompareTo(y.Start);
        if (startComparison != 0)
        {
            return startComparison;
        }

        return x.End.CompareTo(y.End);
    }
}
