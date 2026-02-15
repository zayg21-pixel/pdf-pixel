using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PdfPixel.Demo.Wpf
{
    /// <summary>
    /// Implements Windows Explorer-style natural string comparison.
    /// </summary>
    public class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y)) { return 0; }
            if (x == null) { return -1; }
            if (y == null) { return 1; }

            var xParts = SplitIntoParts(x);
            var yParts = SplitIntoParts(y);
            int minLen = xParts.Length < yParts.Length ? xParts.Length : yParts.Length;

            for (int i = 0; i < minLen; i++)
            {
                int result = ComparePart(xParts[i], yParts[i]);
                if (result != 0) { return result; }
            }
            return xParts.Length.CompareTo(yParts.Length);
        }

        private static string[] SplitIntoParts(string input)
        {
            return Regex.Split(input, "(\\d+)");
        }

        private static int ComparePart(string a, string b)
        {
            bool aIsNumber = int.TryParse(a, out int aNum);
            bool bIsNumber = int.TryParse(b, out int bNum);
            if (aIsNumber && bIsNumber)
            {
                return aNum.CompareTo(bNum);
            }
            return string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
