using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Ccitt
{
    /// <summary>
    /// Helpers for managing and querying change lists (arrays of run boundary x positions) for CCITT 2-D decoding.
    /// Each list ends with a sentinel entry equal to the line width.
    /// </summary>
    internal static class CcittChangeList
    {
        /// <summary>
        /// Find the first change position strictly greater than a0. Returns a0 if none found (caller should clamp).
        /// </summary>
        public static int FindA1(List<int> changes, int a0)
        {
            for (int index = 0; index < changes.Count; index++)
            {
                if (changes[index] > a0)
                {
                    return changes[index];
                }
            }
            return a0;
        }

        /// <summary>
        /// Find the index in the change list whose value is the first strictly greater than start.
        /// Returns -1 when no such change exists.
        /// </summary>
        public static int FindNextChangeIndex(List<int> changes, int start)
        {
            for (int index = 0; index < changes.Count; index++)
            {
                if (changes[index] > start)
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// PASS mode (T.6): advance a0 to b2 where b1 is the first change > a0 on the reference line and b2 the next change after b1.
        /// (Color at a0 does not change.)
        /// </summary>
        public static bool TryComputePassAdvance(List<int> refChanges, int width, int a0, bool _a0Color, out int newA0)
        {
            newA0 = width;
            if (refChanges == null || refChanges.Count == 0)
            {
                return false;
            }

            // Find b1 (first change strictly greater than a0)
            int b1Index = -1;
            for (int i = 0; i < refChanges.Count; i++)
            {
                int v = refChanges[i];
                if (v > a0)
                {
                    b1Index = i;
                    break;
                }
            }
            if (b1Index < 0)
            {
                return false; // no change to the right of a0
            }
            // Skip possible duplicate change positions (defensive) to locate b2
            int b2Index = b1Index + 1;
            while (b2Index < refChanges.Count && refChanges[b2Index] == refChanges[b1Index])
            {
                b2Index++;
            }
            if (b2Index >= refChanges.Count)
            {
                return false; // no second change (should not happen if sentinel present)
            }
            newA0 = refChanges[b2Index];
            if (newA0 > width)
            {
                newA0 = width;
            }
            return true;
        }
    }
}
