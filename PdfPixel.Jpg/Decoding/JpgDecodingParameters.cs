using System;
using PdfPixel.Jpg.Model;

namespace PdfPixel.Jpg.Decoding;

/// <summary>
/// Immutable container for JPEG decoding parameters derived from the header.
/// Extracted once to avoid recomputing sizing and sampling invariants during decoding.
///
/// MCU geometry follows the JPEG spec (T.81, §A.2):
///   - For single-component images the scan is always non-interleaved; each MCU contains
///     exactly one data unit regardless of the component's H/V sampling factors.
///     Effective HMax = VMax = 1, McuWidth = McuHeight = 8.
///   - For multi-component images the scan is interleaved; MCU dimensions are derived from
///     the maximum H/V sampling factors across all components.
///
/// ComponentBlocksH/V store the effective per-component block counts per MCU dimension
/// (1 for single-component images, actual H/V for multi-component). All decoders must use
/// these values rather than reading sampling factors directly from the header.
/// </summary>
public sealed class JpgDecodingParameters
{
    public JpgDecodingParameters(JpgHeader header)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }
        if (header.ComponentCount <= 0 || header.Components == null || header.Components.Count != header.ComponentCount)
        {
            throw new ArgumentException("Invalid header components.", nameof(header));
        }

        int hMax = 1;
        int vMax = 1;

        // Single-component images are always non-interleaved: one block per MCU regardless of H/V.
        bool singleComponent = header.ComponentCount == 1;
        if (!singleComponent)
        {
            for (int i = 0; i < header.Components.Count; i++)
            {
                var c = header.Components[i];
                if (c.HorizontalSamplingFactor > hMax)
                {
                    hMax = c.HorizontalSamplingFactor;
                }
                if (c.VerticalSamplingFactor > vMax)
                {
                    vMax = c.VerticalSamplingFactor;
                }
            }
        }

        HMax = hMax;
        VMax = vMax;
        McuWidth = 8 * hMax;
        McuHeight = 8 * vMax;
        McuColumns = (header.Width + McuWidth - 1) / McuWidth;
        McuRows = (header.Height + McuHeight - 1) / McuHeight;
        UpsampledBlocksPerMcu = hMax * vMax;
        OutputStride = checked(header.Width * header.ComponentCount);

        ComponentBlocksH = new int[header.ComponentCount];
        ComponentBlocksV = new int[header.ComponentCount];
        BlocksPerMcu = new int[header.ComponentCount];
        TotalBlocksPerBand = new int[header.ComponentCount];
        bool upsamplingNeeded = false;

        for (int ci = 0; ci < header.ComponentCount; ci++)
        {
            int h = singleComponent ? 1 : header.Components[ci].HorizontalSamplingFactor;
            int v = singleComponent ? 1 : header.Components[ci].VerticalSamplingFactor;
            ComponentBlocksH[ci] = h;
            ComponentBlocksV[ci] = v;
            int blocks = h * v;
            BlocksPerMcu[ci] = blocks;
            TotalBlocksPerBand[ci] = McuColumns * blocks;
            if (h != hMax || v != vMax)
            {
                upsamplingNeeded = true;
            }
        }

        NeedsUpsampling = upsamplingNeeded;
    }

    public int HMax { get; }
    public int VMax { get; }
    public int McuWidth { get; }
    public int McuHeight { get; }
    public int McuColumns { get; }
    public int McuRows { get; }
    public int OutputStride { get; }
    public int UpsampledBlocksPerMcu { get; }
    public int[] ComponentBlocksH { get; }
    public int[] ComponentBlocksV { get; }
    public int[] BlocksPerMcu { get; }
    public int[] TotalBlocksPerBand { get; }
    public bool NeedsUpsampling { get; }
}
