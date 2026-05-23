using PdfPixel.Jpx.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Pre-computed layout for one (resolution, subbandIndex) pair within the precinct state array.
/// </summary>
internal struct JpxSubbandLayout
{
    public int BaseOffset;
    public int PrecinctsX;
    public int PrecinctsY;
    public int PrecinctStride; // = PrecinctsX * PrecinctsY
}

/// <summary>
/// Per-precinct state that persists across quality layers during packet header parsing.
/// Tracks code-block inclusion, zero bit-planes, and accumulated code-block data
/// per ITU-T T.800 Annex B.
/// </summary>
internal sealed class JpxPrecinctState
{
    /// <summary>
    /// Tag tree for tracking first-inclusion layer of each code-block.
    /// </summary>
    public JpxTagTree InclusionTree;

    /// <summary>
    /// Tag tree for decoding zero bit-plane counts on first inclusion.
    /// </summary>
    public JpxTagTree ZeroBitPlaneTree;

    /// <summary>
    /// Persistent code-blocks that accumulate data across layers (flat array, indexed by cbx * CodeBlocksY + cby).
    /// </summary>
    public JpxCodeBlock[] CodeBlocks;

    /// <summary>
    /// Number of code-blocks in the horizontal direction within this precinct.
    /// </summary>
    public int CodeBlocksX;

    /// <summary>
    /// Number of code-blocks in the vertical direction within this precinct.
    /// </summary>
    public int CodeBlocksY;

    /// <summary>
    /// Absolute code-block X index within the subband where this precinct starts.
    /// </summary>
    public int CodeBlockStartX;

    /// <summary>
    /// Absolute code-block Y index within the subband where this precinct starts.
    /// </summary>
    public int CodeBlockStartY;

    /// <summary>
    /// Start X coordinate of the precinct-subband intersection.
    /// </summary>
    public int SubbandX0;

    /// <summary>
    /// Start Y coordinate of the precinct-subband intersection.
    /// </summary>
    public int SubbandY0;

    /// <summary>
    /// End X coordinate of the precinct-subband intersection.
    /// </summary>
    public int SubbandX1;

    /// <summary>
    /// End Y coordinate of the precinct-subband intersection.
    /// </summary>
    public int SubbandY1;

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
        var (precinctWidth, precinctHeight) = JpxPrecinctHelper.GetPrecinctSize(
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

        return new JpxPrecinctState
        {
            InclusionTree = new JpxTagTree(codeBlocksX, codeBlocksY),
            ZeroBitPlaneTree = new JpxTagTree(codeBlocksX, codeBlocksY),
            CodeBlocks = new JpxCodeBlock[codeBlocksX * codeBlocksY],
            CodeBlocksX = codeBlocksX,
            CodeBlocksY = codeBlocksY,
            CodeBlockStartX = codeBlockStartX,
            CodeBlockStartY = codeBlockStartY,
            SubbandX0 = s0x,
            SubbandY0 = s0y,
            SubbandX1 = s1x,
            SubbandY1 = s1y
        };
    }

    /// <summary>
    /// Integer ceiling division.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CeilDiv(int numerator, int denominator)
    {
        return (numerator + denominator - 1) / denominator;
    }
}
