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
        int codeBlockWidth,
        int codeBlockHeight,
        int bandX0,
        int bandY0,
        int precinctX0,
        int precinctY0,
        int precinctX1,
        int precinctY1)
    {
        InclusionTree = inclusionTree;
        ZeroBitPlaneTree = zeroBitPlaneTree;
        CodeBlocks = codeBlocks;
        CodeBlocksX = codeBlocksX;
        CodeBlocksY = codeBlocksY;
        CodeBlockStartX = codeBlockStartX;
        CodeBlockStartY = codeBlockStartY;
        CodeBlockWidth = codeBlockWidth;
        CodeBlockHeight = codeBlockHeight;
        BandX0 = bandX0;
        BandY0 = bandY0;
        PrecinctX0 = precinctX0;
        PrecinctY0 = precinctY0;
        PrecinctX1 = precinctX1;
        PrecinctY1 = precinctY1;
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
    /// Index of this precinct's first code-block column in the subband's code-block partition,
    /// which is anchored at the subband coordinate origin rather than at the precinct.
    /// </summary>
    public int CodeBlockStartX { get; }

    /// <summary>
    /// Index of this precinct's first code-block row in the subband's code-block partition.
    /// </summary>
    public int CodeBlockStartY { get; }

    /// <summary>
    /// Width of one cell of the code-block partition, in subband coordinates.
    /// </summary>
    public int CodeBlockWidth { get; }

    /// <summary>
    /// Height of one cell of the code-block partition, in subband coordinates.
    /// </summary>
    public int CodeBlockHeight { get; }

    /// <summary>
    /// Subband coordinate of the subband's left edge, used to place code-blocks relative to it.
    /// </summary>
    public int BandX0 { get; }

    /// <summary>
    /// Subband coordinate of the subband's top edge.
    /// </summary>
    public int BandY0 { get; }

    /// <summary>
    /// Left edge of the precinct-subband intersection, in subband coordinates.
    /// </summary>
    public int PrecinctX0 { get; }

    /// <summary>
    /// Top edge of the precinct-subband intersection, in subband coordinates.
    /// </summary>
    public int PrecinctY0 { get; }

    /// <summary>
    /// Right edge of the precinct-subband intersection, in subband coordinates.
    /// </summary>
    public int PrecinctX1 { get; }

    /// <summary>
    /// Bottom edge of the precinct-subband intersection, in subband coordinates.
    /// </summary>
    public int PrecinctY1 { get; }

    /// <summary>
    /// Creates a precinct state for the given resolution, subband, and precinct position.
    /// Computes subband bounds, precinct projection, and code-block grid per ITU-T T.800 Annex B,
    /// all in the subband's own coordinate system so that partitions stay anchored to the
    /// reference grid rather than to the tile.
    /// </summary>
    public static JpxPrecinctState Create(
        int resolution,
        int subbandIndex,
        int precinctX,
        int precinctY,
        in JpxRectangle tileBounds,
        JpxCodingStyle codingStyle)
    {
        int decompositionLevels = codingStyle.DecompositionLevels;
        var subbandType = (JpxSubbandType)subbandIndex;

        int bandLevel = JpxBandGeometry.GetBandLevel(resolution, decompositionLevels);
        int bandOffsetX = JpxBandGeometry.GetBandOffsetX(resolution, subbandType);
        int bandOffsetY = JpxBandGeometry.GetBandOffsetY(resolution, subbandType);

        int bandX0 = JpxBandGeometry.GetBandCoordinate(tileBounds.X, bandLevel, bandOffsetX);
        int bandY0 = JpxBandGeometry.GetBandCoordinate(tileBounds.Y, bandLevel, bandOffsetY);
        int bandX1 = JpxBandGeometry.GetBandCoordinate(tileBounds.Right, bandLevel, bandOffsetX);
        int bandY1 = JpxBandGeometry.GetBandCoordinate(tileBounds.Bottom, bandLevel, bandOffsetY);

        // Precincts partition the resolution grid; for resolutions above the lowest, one step of
        // that partition covers two subband samples, so its size halves in subband coordinates.
        (int precinctWidth, int precinctHeight) = JpxPrecinctHelper.GetPrecinctSize(resolution, codingStyle);
        int subbandPrecinctWidth = Math.Max((resolution == 0) ? precinctWidth : precinctWidth / 2, 1);
        int subbandPrecinctHeight = Math.Max((resolution == 0) ? precinctHeight : precinctHeight / 2, 1);

        // The precinct grid is anchored at the subband origin, so the first precinct starts at
        // the partition cell containing bandX0 rather than at bandX0 itself.
        int firstPrecinctX = FloorToMultiple(bandX0, subbandPrecinctWidth);
        int firstPrecinctY = FloorToMultiple(bandY0, subbandPrecinctHeight);

        int precinctStartX = firstPrecinctX + (precinctX * subbandPrecinctWidth);
        int precinctStartY = firstPrecinctY + (precinctY * subbandPrecinctHeight);

        int intersectionX0 = Math.Max(precinctStartX, bandX0);
        int intersectionY0 = Math.Max(precinctStartY, bandY0);
        int intersectionX1 = Math.Min(precinctStartX + subbandPrecinctWidth, bandX1);
        int intersectionY1 = Math.Min(precinctStartY + subbandPrecinctHeight, bandY1);

        // A code-block never spans more than one precinct, so the partition cell is clamped to it.
        int codeBlockWidth = Math.Min(codingStyle.CodeBlockWidth, subbandPrecinctWidth);
        int codeBlockHeight = Math.Min(codingStyle.CodeBlockHeight, subbandPrecinctHeight);

        int codeBlocksX;
        int codeBlocksY;
        int codeBlockStartX = 0;
        int codeBlockStartY = 0;

        if (intersectionX1 <= intersectionX0 || intersectionY1 <= intersectionY0)
        {
            codeBlocksX = 0;
            codeBlocksY = 0;
        }
        else
        {
            // The code-block partition is anchored at the subband origin too, so the leading
            // block of a precinct that does not start on a cell boundary is a partial one.
            codeBlockStartX = FloorDivide(intersectionX0, codeBlockWidth);
            codeBlockStartY = FloorDivide(intersectionY0, codeBlockHeight);
            codeBlocksX = FloorDivide(intersectionX1 - 1, codeBlockWidth) - codeBlockStartX + 1;
            codeBlocksY = FloorDivide(intersectionY1 - 1, codeBlockHeight) - codeBlockStartY + 1;
        }

        return new JpxPrecinctState(
            new JpxTagTree(codeBlocksX, codeBlocksY),
            new JpxTagTree(codeBlocksX, codeBlocksY),
            new JpxCodeBlock[codeBlocksX * codeBlocksY],
            codeBlocksX,
            codeBlocksY,
            codeBlockStartX,
            codeBlockStartY,
            codeBlockWidth,
            codeBlockHeight,
            bandX0,
            bandY0,
            intersectionX0,
            intersectionY0,
            intersectionX1,
            intersectionY1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FloorToMultiple(int value, int multiple) => FloorDivide(value, multiple) * multiple;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FloorDivide(int value, int divisor)
    {
        int quotient = value / divisor;

        if (value < 0 && quotient * divisor != value)
        {
            quotient--;
        }

        return quotient;
    }
}
