using System;
using PdfPixel.Imaging.Jpg.Model;

namespace PdfPixel.Imaging.Jpg.Decoding;

/// <summary>
/// Immutable container for static JPEG decoding parameters derived from the header.
/// Extracted once to avoid recomputing sizing and sampling invariants during decoding.
/// For images where all components have identical sampling factors > (1,1), normalizes 
/// the decoding parameters to use (1,1) sampling factors for correct spatial processing.
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

        // Calculate original sampling factors
        int originalHMax = 1;
        int originalVMax = 1;
        for (int i = 0; i < header.Components.Count; i++)
        {
            var c = header.Components[i];
            if (c.HorizontalSamplingFactor > originalHMax)
            {
                originalHMax = c.HorizontalSamplingFactor;
            }
            if (c.VerticalSamplingFactor > originalVMax)
            {
                originalVMax = c.VerticalSamplingFactor;
            }
        }

        // Determine if normalization is needed
        bool needsNormalization = false;
        
        if (header.ComponentCount == 1)
        {
            needsNormalization = originalHMax > 1 || originalVMax > 1;
        }
        else if (originalHMax > 1 || originalVMax > 1)
        {
            bool allComponentsHaveSameFactors = true;
            for (int i = 0; i < header.Components.Count; i++)
            {
                var c = header.Components[i];
                if (c.HorizontalSamplingFactor != originalHMax || c.VerticalSamplingFactor != originalVMax)
                {
                    allComponentsHaveSameFactors = false;
                    break;
                }
            }
            needsNormalization = allComponentsHaveSameFactors;
        }

        // Set the correct decoding parameters based on normalization requirement
        int hMax, vMax;
        if (needsNormalization)
        {
            // Normalize to (1,1) - treat each 8x8 block as its own MCU
            hMax = 1;
            vMax = 1;
        }
        else
        {
            // Use original sampling factors
            hMax = originalHMax;
            vMax = originalVMax;
        }

        HMax = hMax;
        VMax = vMax;
        McuWidth = 8 * hMax;
        McuHeight = 8 * vMax;
        McuColumns = (header.Width + McuWidth - 1) / McuWidth;
        McuRows = (header.Height + McuHeight - 1) / McuHeight;
        UpsampledBlocksPerMcu = hMax * vMax;
        OutputStride = checked(header.Width * header.ComponentCount);

        BlocksPerMcu = new int[header.ComponentCount];
        TotalBlocksPerBand = new int[header.ComponentCount];
        bool upsamplingNeeded = false;

        for (int ci = 0; ci < header.ComponentCount; ci++)
        {
            // For normalization cases, use (1,1) factors; otherwise use original factors
            int h = needsNormalization ? 1 : header.Components[ci].HorizontalSamplingFactor;
            int v = needsNormalization ? 1 : header.Components[ci].VerticalSamplingFactor;
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
