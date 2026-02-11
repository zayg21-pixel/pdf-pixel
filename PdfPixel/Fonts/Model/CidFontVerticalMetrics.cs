using System.Collections.Generic;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Parses and exposes vertical metrics for CID descendant fonts per PDF spec.
/// DW2 defines default vertical metrics as [V1y, W1y].
/// W2 defines per-CID vertical metrics using groups [W1y, V1x, V1y] or ranged entries.
/// All values are converted to user space units using VerticalToUserSpaceCoeff.
/// </summary>
public class CidFontVerticalMetrics
{
    /// <summary>
    /// Conversion coefficient from glyph units to user space units.
    /// </summary>
    public const float VerticalToUserSpaceCoeff = 0.001f;

    /// <summary>
    /// Default vertical advance (W1y). Set from DW2 or falls back to -1000 * 0.001.
    /// </summary>
    public float DefaultW1 { get; set; }

    /// <summary>
    /// Default vertical origin Y displacement (V1y). Set from DW2 or falls back to 880 * 0.001.
    /// </summary>
    public float DefaultV1 { get; set; }

    /// <summary>
    /// Per-CID vertical metrics map. Stores W1y, V1y and optional V1x when provided by W2.
    /// </summary>
    public Dictionary<uint, VerticalMetric> CidVerticalMetrics { get; set; }

    /// <summary>
    /// Returns vertical metrics for the specified CID. Falls back to defaults when no per-CID entry exists.
    /// </summary>
    public VerticalMetric GetMetrics(uint cid)
    {
        if (CidVerticalMetrics != null && CidVerticalMetrics.TryGetValue(cid, out var m))
        {
            return m;
        }

        return new VerticalMetric(DefaultW1, DefaultV1);
    }

    /// <summary>
    /// Parse vertical metrics from the CID font dictionary according to PDF spec.
    /// DW2: [V1y, W1y]
    /// W2: c [W1y V1x V1y] ... or cFirst cLast W1y V1x V1y
    /// </summary>
    public static CidFontVerticalMetrics Parse(PdfDictionary fontDictionary)
    {
        var metrics = new Dictionary<uint, VerticalMetric>();

        // DW2 defaults
        var dw2Array = fontDictionary.GetArray(PdfTokens.DW2Key);
        float defaultW1;
        float defaultV1;
        if (dw2Array != null && dw2Array.Count >= 2)
        {
            defaultV1 = dw2Array.GetFloatOrDefault(0) * VerticalToUserSpaceCoeff; // V1y
            defaultW1 = dw2Array.GetFloatOrDefault(1) * VerticalToUserSpaceCoeff; // W1y
        }
        else
        {
            defaultV1 = 880f * VerticalToUserSpaceCoeff;     // V1y fallback
            defaultW1 = -1000f * VerticalToUserSpaceCoeff;    // W1y fallback
        }

        // W2 overrides
        var w2Array = fontDictionary.GetArray(PdfTokens.W2Key);
        if (w2Array != null)
        {
            int i = 0;
            while (i < w2Array.Count)
            {
                var first = w2Array.GetValue(i++);
                if (first == null)
                {
                    break;
                }

                uint firstCid = (uint)first.AsInteger();
                var second = w2Array.GetValue(i++);
                if (second == null)
                {
                    break;
                }

                if (second.Type == PdfValueType.Array)
                {
                    // Individual successive CIDs starting at firstCid
                    var arr = second.AsArray();
                    int j = 0;
                    uint currentCid = firstCid;
                    while (j + 2 < arr.Count)
                    {
                        float w1y = arr.GetFloatOrDefault(j++) * VerticalToUserSpaceCoeff; // W1y
                        float v1x = arr.GetFloatOrDefault(j++) * VerticalToUserSpaceCoeff; // V1x
                        float v1y = arr.GetFloatOrDefault(j++) * VerticalToUserSpaceCoeff; // V1y
                        metrics[currentCid++] = new VerticalMetric(w1y, v1y, v1x);
                    }
                }
                else
                {
                    // Range: firstCid..lastCid
                    uint lastCid = (uint)second.AsInteger();
                    if (i + 2 >= w2Array.Count)
                    {
                        break;
                    }

                    float w1y = w2Array.GetFloatOrDefault(i++) * VerticalToUserSpaceCoeff; // W1y
                    float v1x = w2Array.GetFloatOrDefault(i++) * VerticalToUserSpaceCoeff; // V1x
                    float v1y = w2Array.GetFloatOrDefault(i++) * VerticalToUserSpaceCoeff; // V1y

                    for (uint cid = firstCid; cid <= lastCid; cid++)
                    {
                        metrics[cid] = new VerticalMetric(w1y, v1y, v1x);
                    }
                }
            }
        }

        return new CidFontVerticalMetrics
        {
            DefaultW1 = defaultW1,
            DefaultV1 = defaultV1,
            CidVerticalMetrics = metrics
        };
    }
}
