using PdfReader.Fonts.Mapping;
using System.Collections.Generic;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Fonts.Types
{
    /// <summary>
    /// Font width information for PDF fonts.
    /// Handles both simple and CID fonts, and determines if explicit widths are defined.
    /// </summary>
    public class PdfFontWidths
    {
        /// <summary>
        /// First character code for simple fonts. Null if not defined.
        /// </summary>
        public uint? FirstChar { get; set; }

        /// <summary>
        /// Last character code for simple fonts. Null if not defined.
        /// </summary>
        public uint? LastChar { get; set; }

        /// <summary>
        /// Widths array for simple fonts. Null if not defined.
        /// </summary>
        public float[] Widths { get; set; }

        /// <summary>
        /// Default width for CID fonts. Null if not defined.
        /// </summary>
        public float? DefaultWidth { get; set; }

        /// <summary>
        /// Explicit CID widths for CID fonts. Null if not defined.
        /// </summary>
        public Dictionary<uint, float> CIDWidths { get; set; }

        /// <summary>
        /// Returns true if explicit widths are defined for the given code.
        /// </summary>
        public bool HasExplicitWidth(PdfCharacterCode code)
        {
            uint cid = (uint)code;
            if (Widths != null && FirstChar.HasValue && LastChar.HasValue && cid >= FirstChar.Value && cid <= LastChar.Value)
            {
                uint index = cid - FirstChar.Value;
                if (index < Widths.Length)
                {
                    return true;
                }
            }
            if (CIDWidths != null && CIDWidths.ContainsKey(cid))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the width for the given character code. Returns explicit width if defined, otherwise null.
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
            if (CIDWidths != null && CIDWidths.TryGetValue(cid, out float width))
            {
                return width;
            }
            return null;
        }

        /// <summary>
        /// Parses font widths for a single-byte font from a PDF dictionary.
        /// </summary>
        /// <param name="fontDictionary">PDF dictionary containing the font definition.</param>
        /// <returns>Parsed PdfFontWidths instance.</returns>
        public static PdfFontWidths ParseSingleByteFontWidths(PdfDictionary fontDictionary)
        {
            var firstChar = (uint?)fontDictionary.GetInteger(PdfTokens.FirstCharKey);
            var lastChar = (uint?)fontDictionary.GetInteger(PdfTokens.LastCharKey);
            var widthsArray = fontDictionary.GetArray(PdfTokens.WidthsKey)?.GetFloatArray();

            return new PdfFontWidths
            {
                FirstChar = firstChar,
                LastChar = lastChar,
                Widths = widthsArray
            };
        }

        /// <summary>
        /// Parses font widths for a CID font from a PDF dictionary.
        /// Handles both individual and ranged widths as per PDF spec.
        /// </summary>
        /// <param name="fontDictionary">PDF dictionary containing the font definition.</param>
        /// <returns>Parsed PdfFontWidths instance.</returns>
        public static PdfFontWidths ParseCidFontWidths(PdfDictionary fontDictionary)
        {
            var cidWidths = new Dictionary<uint, float>();
            var wArray = fontDictionary.GetArray(PdfTokens.WKey);
            if (wArray != null)
            {
                int i = 0;
                while (i < wArray.Count)
                {
                    var first = wArray.GetValue(i++);
                    if (first == null) { break; }
                    uint firstCid = (uint)first.AsInteger();
                    var second = wArray.GetValue(i++);
                    if (second == null) { break; }
                    if (second.Type == PdfValueType.Array)
                    {
                        // Individual widths for a range
                        var widthsArr = second.AsArray();
                        for (int j = 0; j < widthsArr.Count; j++)
                        {
                            cidWidths[firstCid + (uint)j] = widthsArr.GetValue(j).AsFloat();
                        }
                    }
                    else
                    {
                        // Range: firstCid to secondCid, all have the same width
                        uint lastCid = (uint)second.AsInteger();
                        var widthVal = wArray.GetValue(i++);
                        if (widthVal == null) { break; }
                        float width = widthVal.AsFloat();
                        for (uint cid = firstCid; cid <= lastCid; cid++)
                        {
                            cidWidths[cid] = width;
                        }
                    }
                }
            }
            float? defaultWidth = fontDictionary.GetFloat(PdfTokens.DWKey);
            return new PdfFontWidths
            {
                CIDWidths = cidWidths,
                DefaultWidth = defaultWidth
            };
        }
    }
}