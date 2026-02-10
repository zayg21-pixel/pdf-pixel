using System.Collections.Generic;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Comparer for <see cref="CidRangeMap"/> that orders ranges first by length,
/// then by start code value, and finally by end code value. This ordering enables
/// binary search by code value when ranges are kept sorted.
/// </summary>
internal sealed class CidRangeComparer : IComparer<CidRangeMap>
{
    /// <summary>
    /// Singleton instance to avoid allocations.
    /// </summary>
    public static readonly CidRangeComparer Instance = new CidRangeComparer();

    private CidRangeComparer()
    {
    }

    public int Compare(CidRangeMap x, CidRangeMap y)
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
