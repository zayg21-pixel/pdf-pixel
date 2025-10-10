using System.Collections.Generic;

namespace PdfReader.Fonts.Types
{
    // Font width information
    public class PdfFontWidths
    {
        public int FirstChar { get; set; }
        public int LastChar { get; set; }
        public float[] Widths { get; set; } = [];
        public float DefaultWidth { get; set; } = 1000; // Default for CID fonts
        public Dictionary<int, float> CIDWidths { get; set; } = new Dictionary<int, float>(); // For CID fonts

        public float GetWidth(int charCode)
        {
            // For simple fonts with FirstChar/LastChar/Widths
            if (Widths.Length > 0 && charCode >= FirstChar && charCode <= LastChar)
            {
                int index = charCode - FirstChar;
                if (index >= 0 && index < Widths.Length)
                    return Widths[index];
            }

            // For CID fonts
            if (CIDWidths.TryGetValue(charCode, out float width))
                return width;

            return DefaultWidth;
        }
    }
}