using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts.Mapping
{
    /// <summary>
    /// Utility helpers for working with CMap range storage and lookups.
    /// Contains insertion helpers that keep lists sorted and optimized binary searches.
    /// Marked with aggressive inlining hints for performance-critical paths.
    /// </summary>
    internal static class PdfCMapUtilities
    {
        /// <summary>
        /// Inserts a Unicode range into a list while keeping it sorted using UnicodeRangeComparer.
        /// </summary>
        /// <param name="list">Target list for ranges of a specific code length.</param>
        /// <param name="range">Range to insert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertUnicodeRangeSorted(List<UnicodeRangeMap> list, UnicodeRangeMap range)
        {
            if (list == null)
            {
                return;
            }

            int index = list.BinarySearch(range, UnicodeRangeComparer.Instance);
            if (index < 0)
            {
                index = ~index;
            }
            list.Insert(index, range);
        }

        /// <summary>
        /// Inserts a CID range into a list while keeping it sorted using CidRangeComparer.
        /// </summary>
        /// <param name="list">Target list for ranges of a specific code length.</param>
        /// <param name="range">Range to insert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertCidRangeSorted(List<CidRangeMap> list, CidRangeMap range)
        {
            if (list == null)
            {
                return;
            }

            int index = list.BinarySearch(range, CidRangeComparer.Instance);
            if (index < 0)
            {
                index = ~index;
            }
            list.Insert(index, range);
        }

        /// <summary>
        /// Binary search for a code value in the sorted Unicode range list.
        /// Returns the index of the matching range or -1 if not found.
        /// </summary>
        /// <param name="ranges">Sorted list of ranges for a given length.</param>
        /// <param name="value">Code value interpreted as big-endian.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearchUnicode(List<UnicodeRangeMap> ranges, uint value)
        {
            int low = 0;
            int high = ranges.Count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                var r = ranges[mid];
                if (value < r.Start)
                {
                    high = mid - 1;
                }
                else if (value > r.End)
                {
                    low = mid + 1;
                }
                else
                {
                    return mid;
                }
            }
            return -1;
        }

        /// <summary>
        /// Binary search for a code value in the sorted CID range list.
        /// Returns the index of the matching range or -1 if not found.
        /// </summary>
        /// <param name="ranges">Sorted list of ranges for a given length.</param>
        /// <param name="value">Code value interpreted as big-endian.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearchCid(List<CidRangeMap> ranges, uint value)
        {
            int low = 0;
            int high = ranges.Count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                var r = ranges[mid];
                if (value < r.Start)
                {
                    high = mid - 1;
                }
                else if (value > r.End)
                {
                    low = mid + 1;
                }
                else
                {
                    return mid;
                }
            }
            return -1;
        }
    }
}
