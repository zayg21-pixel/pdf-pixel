using PdfPixel.Jpx.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Per-precinct state that persists across quality layers during packet header parsing.
/// Tracks code-block inclusion, zero bit-planes, and accumulated code-block data
/// per ITU-T T.800 Annex B.
/// </summary>
internal sealed class JpxPrecinctState
{
    public JpxPrecinctState(
        JpxTagTree inclusionTree,
        JpxTagTree zeroBitPlaneTree,
        JpxCodeBlock[] codeBlocks,
        int codeBlocksX,
        int codeBlocksY,
        int codeBlockStartX,
        int codeBlockStartY,
        int subbandX0,
        int subbandY0,
        int subbandX1,
        int subbandY1)
    {
        InclusionTree = inclusionTree;
        ZeroBitPlaneTree = zeroBitPlaneTree;
        CodeBlocks = codeBlocks;
        CodeBlocksX = codeBlocksX;
        CodeBlocksY = codeBlocksY;
        CodeBlockStartX = codeBlockStartX;
        CodeBlockStartY = codeBlockStartY;
        SubbandX0 = subbandX0;
        SubbandY0 = subbandY0;
        SubbandX1 = subbandX1;
        SubbandY1 = subbandY1;
    }

    /// <summary>
    /// Tag tree for tracking first-inclusion layer of each code-block.
    /// </summary>
    public JpxTagTree InclusionTree { get; }

    /// <summary>
    /// Tag tree for decoding zero bit-plane counts on first inclusion.
    /// </summary>
    public JpxTagTree ZeroBitPlaneTree { get; }

    /// <summary>
    /// Persistent code-blocks that accumulate data across layers (flat array, indexed by cbx * CodeBlocksY + cby).
    /// </summary>
    public JpxCodeBlock[] CodeBlocks { get; }

    /// <summary>
    /// Number of code-blocks in the horizontal direction within this precinct.
    /// </summary>
    public int CodeBlocksX { get; }

    /// <summary>
    /// Number of code-blocks in the vertical direction within this precinct.
    /// </summary>
    public int CodeBlocksY { get; }

    /// <summary>
    /// Absolute code-block X index within the subband where this precinct starts.
    /// </summary>
    public int CodeBlockStartX { get; }

    /// <summary>
    /// Absolute code-block Y index within the subband where this precinct starts.
    /// </summary>
    public int CodeBlockStartY { get; }

    /// <summary>
    /// Start X coordinate of the precinct-subband intersection.
    /// </summary>
    public int SubbandX0 { get; }

    /// <summary>
    /// Start Y coordinate of the precinct-subband intersection.
    /// </summary>
    public int SubbandY0 { get; }

    /// <summary>
    /// End X coordinate of the precinct-subband intersection.
    /// </summary>
    public int SubbandX1 { get; }

    /// <summary>
    /// End Y coordinate of the precinct-subband intersection.
    /// </summary>
    public int SubbandY1 { get; }

    /// <summary>
    /// Creates a precinct state for the given resolution, subband, and precinct position.
    /// Computes subband dimensions, precinct projection, and code-block grid per ITU-T T.800 Annex B.
    /// </summary>
    public static JpxPrecinctState Create(
        int resolution,
        int subbandIndex,
        int precinctX,
        int precinctY,
        int tileWidth,
        int tileHeight,
        JpxCodingStyle codingStyle)
    {
        int decompositionLevels = codingStyle.DecompositionLevels;
        int subbandWidth;
        int subbandHeight;

        if (resolution == 0)
        {
            subbandWidth = CeilDiv(tileWidth, 1 << decompositionLevels);
            subbandHeight = CeilDiv(tileHeight, 1 << decompositionLevels);
        }
        else
        {
            int level = decompositionLevels - resolution;
            int resWidth = CeilDiv(tileWidth, 1 << level);
            int prevResWidth = CeilDiv(tileWidth, 1 << (level + 1));
            int resHeight = CeilDiv(tileHeight, 1 << level);
            int prevResHeight = CeilDiv(tileHeight, 1 << (level + 1));

            if (subbandIndex == 0) // HL
            {
                subbandWidth = resWidth - prevResWidth;
                subbandHeight = prevResHeight;
            }
            else if (subbandIndex == 1) // LH
            {
                subbandWidth = prevResWidth;
                subbandHeight = resHeight - prevResHeight;
            }
            else // HH
            {
                subbandWidth = resWidth - prevResWidth;
                subbandHeight = resHeight - prevResHeight;
            }
        }

        // Get precinct size at this resolution level
        (int precinctWidth, int precinctHeight) = JpxPrecinctHelper.GetPrecinctSize(
            resolution, codingStyle);

        // For resolution > 0, precinct size in subband coordinates is halved
        int subbandPrecinctWidth = (resolution == 0) ? precinctWidth : precinctWidth / 2;
        int subbandPrecinctHeight = (resolution == 0) ? precinctHeight : precinctHeight / 2;

        // Ensure minimum of 1
        subbandPrecinctWidth = Math.Max(subbandPrecinctWidth, 1);
        subbandPrecinctHeight = Math.Max(subbandPrecinctHeight, 1);

        // Compute precinct projection into subband and clip against subband bounds
        int p0x = precinctX * subbandPrecinctWidth;
        int p0y = precinctY * subbandPrecinctHeight;
        int p1x = p0x + subbandPrecinctWidth;
        int p1y = p0y + subbandPrecinctHeight;

        int s0x = Math.Max(p0x, 0);
        int s1x = Math.Min(p1x, subbandWidth);
        int s0y = Math.Max(p0y, 0);
        int s1y = Math.Min(p1y, subbandHeight);

        int codeBlocksX;
        int codeBlocksY;
        int codeBlockStartX = 0;
        int codeBlockStartY = 0;

        if (s1x <= s0x || s1y <= s0y)
        {
            codeBlocksX = 0;
            codeBlocksY = 0;
        }
        else
        {
            // Code-block grid within the precinct-subband intersection
            int codeBlockWidth = codingStyle.CodeBlockWidth;
            int codeBlockHeight = codingStyle.CodeBlockHeight;

            int lstart = s0x / codeBlockWidth;
            int lend = (s1x - 1) / codeBlockWidth;
            int kstart = s0y / codeBlockHeight;
            int kend = (s1y - 1) / codeBlockHeight;

            codeBlockStartX = lstart;
            codeBlockStartY = kstart;
            codeBlocksX = lend - lstart + 1;
            codeBlocksY = kend - kstart + 1;
        }

        return new JpxPrecinctState(
            new JpxTagTree(codeBlocksX, codeBlocksY),
            new JpxTagTree(codeBlocksX, codeBlocksY),
            new JpxCodeBlock[codeBlocksX * codeBlocksY],
            codeBlocksX,
            codeBlocksY,
            codeBlockStartX,
            codeBlockStartY,
            s0x,
            s0y,
            s1x,
            s1y);
    }

    /// <summary>
    /// Integer ceiling division.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CeilDiv(int numerator, int denominator) => (numerator + denominator - 1) / denominator;
}
