using PdfReader.Fonts.Mapping;
using System.Collections.Generic;

namespace PdfReader.Fonts.Types
{
    // Font width information
    public class PdfFontWidths
    {
        public uint FirstChar { get; set; }
        public uint LastChar { get; set; }
        public float[] Widths { get; set; } = [];
        public float DefaultWidth { get; set; } // Default for CID fonts
        public Dictionary<uint, float> CIDWidths { get; set; } = new Dictionary<uint, float>(); // For CID fonts

        public float GetWidth(PdfCharacterCode code)
        {
            // For simple fonts with FirstChar/LastChar/Widths
            if (Widths.Length > 0 && code >= FirstChar && code <= LastChar)
            {
                uint index = (uint)code - FirstChar;
                if (index >= 0 && index < Widths.Length)
                    return Widths[index];
            }

            // For CID fonts
            if (CIDWidths.TryGetValue(code, out float width))
                return width;

            return DefaultWidth;
        }
    }
}