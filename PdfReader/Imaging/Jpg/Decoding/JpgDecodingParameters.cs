using System;
using PdfReader.Imaging.Jpg.Model;

namespace PdfReader.Imaging.Jpg.Decoding;

/// <summary>
/// Immutable container for static JPEG decoding parameters derived from the header.
/// Extracted once to avoid recomputing sizing and sampling invariants during decoding.
/// </summary>
internal sealed class JpgDecodingParameters
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
        HMax = hMax;
        VMax = vMax;
        McuWidth = 8 * HMax;
        McuHeight = 8 * VMax;
        McuColumns = (header.Width + McuWidth - 1) / McuWidth;
        McuRows = (header.Height + McuHeight - 1) / McuHeight;
        UpsampledBlocksPerMcu = HMax * VMax;
        OutputStride = checked(header.Width * header.ComponentCount);

        BlocksPerMcu = new int[header.ComponentCount];
        TotalBlocksPerBand = new int[header.ComponentCount];
        bool upsamplingNeeded = false;
        for (int ci = 0; ci < header.ComponentCount; ci++)
        {
            int h = header.Components[ci].HorizontalSamplingFactor;
            int v = header.Components[ci].VerticalSamplingFactor;
            int blocks = h * v;
            BlocksPerMcu[ci] = blocks;
            TotalBlocksPerBand[ci] = McuColumns * blocks;
            if (h != HMax || v != VMax)
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
    public int[] BlocksPerMcu { get; }
    public int[] TotalBlocksPerBand { get; }
    public bool NeedsUpsampling { get; }
}
