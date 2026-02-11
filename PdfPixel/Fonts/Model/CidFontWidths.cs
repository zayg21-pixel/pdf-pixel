using System.Collections.Generic;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Font width information for CID fonts (Type0 descendant fonts).
/// Handles both individual and ranged widths as per PDF spec.
/// All widths are stored in user space units (PDF spec: multiply by WidthToUserSpaceCoeff).
/// </summary>
public class CidFontWidths
{
    /// <summary>
    /// Coefficient to convert PDF font units to user space units.
    /// </summary>
    public const float WidthToUserSpaceCoeff = 0.001f;

    /// <summary>
    /// Default width for CID fonts. Null if not defined.
    /// </summary>
    public float? DefaultWidth { get; set; }

    /// <summary>
    /// Explicit CID widths for CID fonts. Null if not defined.
    /// </summary>
    public Dictionary<uint, float> CidWidths { get; set; }

    /// <summary>
    /// Gets the width for the given CID. Returns explicit width if defined, otherwise null.
    /// All widths are returned in user space units.
    /// </summary>
    /// <param name="cid">The CID to get the width for.</param>
    /// <returns>The width for the CID, or null if not defined.</returns>
    public float? GetWidth(uint cid)
    {
        if (CidWidths != null && CidWidths.TryGetValue(cid, out float width))
        {
            return width;
        }
        return null;
    }

    /// <summary>
    /// Parses font widths for a CID font from a PDF dictionary.
    /// Handles both individual and ranged widths as per PDF spec.
    /// All widths are stored in user space units (PDF spec: multiply by WidthToUserSpaceCoeff).
    /// </summary>
    /// <param name="fontDictionary">PDF dictionary containing the font definition.</param>
    /// <returns>Parsed CidFontWidths instance.</returns>
    public static CidFontWidths Parse(PdfDictionary fontDictionary)
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
                        cidWidths[firstCid + (uint)j] = widthsArr.GetFloatOrDefault(j) * WidthToUserSpaceCoeff;
                    }
                }
                else
                {
                    // Range: firstCid to secondCid, all have the same width
                    uint lastCid = (uint)second.AsInteger();
                    float width = wArray.GetFloatOrDefault(i++) * WidthToUserSpaceCoeff;
                    for (uint cid = firstCid; cid <= lastCid; cid++)
                    {
                        cidWidths[cid] = width;
                    }
                }
            }
        }
        float? defaultWidth = fontDictionary.GetFloat(PdfTokens.DWKey);
        if (defaultWidth.HasValue)
        {
            defaultWidth *= WidthToUserSpaceCoeff;
        }
        return new CidFontWidths
        {
            CidWidths = cidWidths,
            DefaultWidth = defaultWidth
        };
    }
}
