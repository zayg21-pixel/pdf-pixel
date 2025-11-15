using PdfReader.Models;
using PdfReader.Fonts.Mapping;
using PdfReader.Text;

namespace PdfReader.Fonts.Types
{
    /// <summary>
    /// Font width information for single-byte fonts (Type1, TrueType, MMType1, Type3).
    /// All widths are stored in user space units (PDF spec: multiply by WidthToUserSpaceCoeff).
    /// </summary>
    public class SingleByteFontWidths
    {
        /// <summary>
        /// Coefficient to convert PDF font units to user space units.
        /// </summary>
        public const float WidthToUserSpaceCoeff = 0.001f;

        /// <summary>
        /// First character code for single-byte fonts. Null if not defined.
        /// </summary>
        public uint? FirstChar { get; set; }

        /// <summary>
        /// Last character code for single-byte fonts. Null if not defined.
        /// </summary>
        public uint? LastChar { get; set; }

        /// <summary>
        /// Widths array for single-byte fonts. Null if not defined.
        /// </summary>
        public float[] Widths { get; set; }

        /// <summary>
        /// Gets the width for the given character code. Returns explicit width if defined, otherwise null.
        /// All widths are returned in user space units.
        /// </summary>
        public float? GetWidth(PdfCharacterCode code)
        {
            uint cid = (uint)code;
            if (Widths != null && FirstChar.HasValue && LastChar.HasValue && cid >= FirstChar.Value && cid <= LastChar.Value)
            {
                uint index = cid - FirstChar.Value;
                if (index < Widths.Length)
                {
                    return Widths[index];
                }
            }
            return null;
        }

        /// <summary>
        /// Parses font widths for a single-byte font from a PDF dictionary.
        /// All widths are stored in user space units (PDF spec: multiply by WidthToUserSpaceCoeff).
        /// </summary>
        /// <param name="fontDictionary">PDF dictionary containing the font definition.</param>
        /// <returns>Parsed SingleByteFontWidths instance.</returns>
        public static SingleByteFontWidths Parse(PdfDictionary fontDictionary)
        {
            var firstChar = (uint?)fontDictionary.GetInteger(PdfTokens.FirstCharKey);
            var lastChar = (uint?)fontDictionary.GetInteger(PdfTokens.LastCharKey);
            var widthsArray = fontDictionary.GetArray(PdfTokens.WidthsKey)?.GetFloatArray();

            if (widthsArray != null)
            {
                for (int i = 0; i < widthsArray.Length; i++)
                {
                    widthsArray[i] *= WidthToUserSpaceCoeff;
                }
            }

            return new SingleByteFontWidths
            {
                FirstChar = firstChar,
                LastChar = lastChar,
                Widths = widthsArray
            };
        }
    }
}
