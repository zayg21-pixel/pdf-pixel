using System;
using System.Collections.Generic;

using PdfPixel.Jpg.Model;

namespace PdfPixel.Jpg.Decoding;

/// <summary>
/// <para>
/// Immutable container for JPEG decoding parameters derived from the header.
/// Extracted once to avoid recomputing sizing and sampling invariants during decoding.
/// </para>
/// <para>
/// MCU geometry follows the JPEG spec (T.81, §A.2):
///   - For single-component images the scan is always non-interleaved; each MCU contains
///     exactly one data unit regardless of the component's H/V sampling factors.
///     Effective HMax = VMax = 1, McuWidth = McuHeight = 8.
///   - For multi-component images the scan is interleaved; MCU dimensions are derived from
///     the maximum H/V sampling factors across all components.
/// </para>
/// <para>
/// ComponentBlocksH/V store the effective per-component block counts per MCU dimension
/// (1 for single-component images, actual H/V for multi-component). All decoders must use
/// these values rather than reading sampling factors directly from the header.
/// </para>
/// <para>
/// A descale factor above 1 reconstructs the image at 1/2, 1/4 or 1/8 of its stored size.
/// Each data unit then covers <see cref="BlockSize"/> output samples per dimension instead of 8,
/// which is what <see cref="ComponentIdctWidth"/>/<see cref="ComponentIdctHeight"/> and
/// <see cref="ComponentReplicationH"/>/<see cref="ComponentReplicationV"/> express per component:
/// a component sampled below the maximum already carries fewer samples than the full grid, so part
/// of the reduction (or all of it, for chroma at the matching factor) is paid by dropping the
/// upsampling rather than by shrinking the inverse transform.
/// </para>
/// <para>
/// Regions of interest narrow the reconstruction — not the entropy decoding, which a serial bitstream
/// forces to run over every data unit. <see cref="ReconstructedMcuColumnStart"/> and
/// <see cref="ReconstructedMcuColumns"/> give the MCU columns a band holds, so the band arrays span
/// the regions rather than the image, and <see cref="IsMcuRowReconstructed"/> reports the MCU rows
/// that reach a band at all.
/// </para>
/// </summary>
public sealed class JpgDecodingParameters
{
    private const int DctSize = 8;

    private readonly bool[]? _reconstructedMcuRows;

    /// <summary>
    /// Derives all decoding geometry from <paramref name="header"/>.
    /// </summary>
    /// <param name="header">Parsed JPEG header.</param>
    /// <param name="descaleFactor">Power-of-two reduction (1, 2, 4 or 8) applied to the reconstructed size.</param>
    /// <param name="regionsOfInterest">Regions, in stored samples, that must be reconstructed. Null reconstructs the whole image.</param>
    public JpgDecodingParameters(JpgHeader header, int descaleFactor = 1, IReadOnlyList<JpgRectangle>? regionsOfInterest = null)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.ComponentCount <= 0 || header.Components == null || header.Components.Count != header.ComponentCount)
        {
            throw new ArgumentException("Invalid header components.", nameof(header));
        }

        if (descaleFactor != 1 && descaleFactor != 2 && descaleFactor != 4 && descaleFactor != 8)
        {
            throw new ArgumentOutOfRangeException(nameof(descaleFactor), "Descale factor must be 1, 2, 4 or 8.");
        }

        int hMax = 1;
        int vMax = 1;

        // Single-component images are always non-interleaved: one block per MCU regardless of H/V.
        bool singleComponent = header.ComponentCount == 1;
        if (!singleComponent)
        {
            for (int i = 0; i < header.Components.Count; i++)
            {
                JpgComponent c = header.Components[i];
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
        DescaleFactor = descaleFactor;
        BlockSize = DctSize / descaleFactor;
        McuWidth = DctSize * hMax;
        McuHeight = DctSize * vMax;
        McuColumns = (header.Width + McuWidth - 1) / McuWidth;
        McuRows = (header.Height + McuHeight - 1) / McuHeight;
        BlockVectorStride = Block8x8F.GetVectorStride(BlockSize);
        BlockVectorLimit = Block8x8F.GetVectorLimit(BlockSize);
        OutputMcuWidth = BlockSize * hMax;
        OutputMcuHeight = BlockSize * vMax;
        OutputWidth = (header.Width + descaleFactor - 1) / descaleFactor;
        OutputHeight = (header.Height + descaleFactor - 1) / descaleFactor;
        UpsampledBlocksPerMcu = hMax * vMax;
        OutputStride = checked(OutputWidth * header.ComponentCount);

        if (regionsOfInterest == null || regionsOfInterest.Count == 0)
        {
            ReconstructedMcuColumnStart = 0;
            ReconstructedMcuColumns = McuColumns;
        }
        else
        {
            ComputeReconstructedColumns(regionsOfInterest, McuWidth, McuColumns, out int reconstructedColumnStart, out int reconstructedColumns);
            ReconstructedMcuColumnStart = reconstructedColumnStart;
            ReconstructedMcuColumns = reconstructedColumns;
            _reconstructedMcuRows = ComputeReconstructedRows(regionsOfInterest, McuHeight, McuRows);
        }

        ComponentBlocksH = new int[header.ComponentCount];
        ComponentBlocksV = new int[header.ComponentCount];
        BlocksPerMcu = new int[header.ComponentCount];
        TotalBlocksPerBand = new int[header.ComponentCount];
        ComponentIdctWidth = new int[header.ComponentCount];
        ComponentIdctHeight = new int[header.ComponentCount];
        ComponentReplicationH = new int[header.ComponentCount];
        ComponentReplicationV = new int[header.ComponentCount];
        var upsamplingNeeded = false;

        for (int ci = 0; ci < header.ComponentCount; ci++)
        {
            int h = singleComponent ? 1 : header.Components[ci].HorizontalSamplingFactor;
            int v = singleComponent ? 1 : header.Components[ci].VerticalSamplingFactor;
            ComponentBlocksH[ci] = h;
            ComponentBlocksV[ci] = v;
            int blocks = h * v;
            BlocksPerMcu[ci] = blocks;
            TotalBlocksPerBand[ci] = ReconstructedMcuColumns * blocks;
            if (h != hMax || v != vMax)
            {
                upsamplingNeeded = true;
            }

            // Output samples one data unit of this component covers, before any replication.
            int footprintWidth = (h > 0) ? BlockSize * hMax / h : BlockSize;
            int footprintHeight = (v > 0) ? BlockSize * vMax / v : BlockSize;
            int idctWidth = LargestTransformSize(footprintWidth);
            int idctHeight = LargestTransformSize(footprintHeight);
            ComponentIdctWidth[ci] = idctWidth;
            ComponentIdctHeight[ci] = idctHeight;
            ComponentReplicationH[ci] = footprintWidth / idctWidth;
            ComponentReplicationV[ci] = footprintHeight / idctHeight;
        }

        NeedsUpsampling = upsamplingNeeded;
    }

    /// <summary>
    /// Maximum horizontal sampling factor across all components.
    /// </summary>
    public int HMax { get; }

    /// <summary>
    /// Maximum vertical sampling factor across all components.
    /// </summary>
    public int VMax { get; }

    /// <summary>
    /// Power-of-two reduction applied to the reconstructed image (1, 2, 4 or 8).
    /// </summary>
    public int DescaleFactor { get; }

    /// <summary>
    /// Edge length in output samples of one reconstructed block (8 / <see cref="DescaleFactor"/>).
    /// </summary>
    public int BlockSize { get; }

    /// <summary>
    /// Step between the block lanes holding samples, once the block is reconstructed at <see cref="BlockSize"/>.
    /// </summary>
    public int BlockVectorStride { get; }

    /// <summary>
    /// One past the last block lane holding samples, once the block is reconstructed at <see cref="BlockSize"/>.
    /// </summary>
    public int BlockVectorLimit { get; }

    /// <summary>
    /// MCU width in stored pixels (8 × <see cref="HMax"/>).
    /// </summary>
    public int McuWidth { get; }

    /// <summary>
    /// MCU height in stored pixels (8 × <see cref="VMax"/>).
    /// </summary>
    public int McuHeight { get; }

    /// <summary>
    /// MCU width in output samples (<see cref="BlockSize"/> × <see cref="HMax"/>).
    /// </summary>
    public int OutputMcuWidth { get; }

    /// <summary>
    /// MCU height in output samples (<see cref="BlockSize"/> × <see cref="VMax"/>).
    /// </summary>
    public int OutputMcuHeight { get; }

    /// <summary>
    /// Width of the reconstructed image in output samples.
    /// </summary>
    public int OutputWidth { get; }

    /// <summary>
    /// Height of the reconstructed image in output samples.
    /// </summary>
    public int OutputHeight { get; }

    /// <summary>
    /// Number of MCU columns needed to cover the full image width.
    /// </summary>
    public int McuColumns { get; }

    /// <summary>
    /// Number of MCU rows needed to cover the full image height.
    /// </summary>
    public int McuRows { get; }

    /// <summary>
    /// First MCU column a reconstructed band holds.
    /// </summary>
    public int ReconstructedMcuColumnStart { get; }

    /// <summary>
    /// Number of MCU columns a reconstructed band holds, starting at <see cref="ReconstructedMcuColumnStart"/>.
    /// </summary>
    public int ReconstructedMcuColumns { get; }

    /// <summary>
    /// True when a band spans the full image width, so a packed row leaves no samples unwritten.
    /// </summary>
    public bool IsFullWidthReconstructed => ReconstructedMcuColumns == McuColumns;

    /// <summary>
    /// Byte stride of one decoded output row (<see cref="OutputWidth"/> × ComponentCount).
    /// </summary>
    public int OutputStride { get; }

    /// <summary>
    /// Number of blocks per MCU after upsampling (<see cref="HMax"/> × <see cref="VMax"/>).
    /// </summary>
    public int UpsampledBlocksPerMcu { get; }

    /// <summary>
    /// Per-component horizontal 8×8 block count per MCU (1 for single-component images, H sampling factor otherwise).
    /// </summary>
    public int[] ComponentBlocksH { get; }

    /// <summary>
    /// Per-component vertical 8×8 block count per MCU (1 for single-component images, V sampling factor otherwise).
    /// </summary>
    public int[] ComponentBlocksV { get; }

    /// <summary>
    /// Per-component number of 8×8 blocks per MCU (<c>ComponentBlocksH[i] × ComponentBlocksV[i]</c>).
    /// </summary>
    public int[] BlocksPerMcu { get; }

    /// <summary>
    /// Per-component total 8×8 blocks in one MCU-height band (<c>ReconstructedMcuColumns × BlocksPerMcu[i]</c>).
    /// </summary>
    public int[] TotalBlocksPerBand { get; }

    /// <summary>
    /// Per-component width of the inverse transform output, in samples (1, 2, 4 or 8).
    /// </summary>
    public int[] ComponentIdctWidth { get; }

    /// <summary>
    /// Per-component height of the inverse transform output, in samples (1, 2, 4 or 8).
    /// </summary>
    public int[] ComponentIdctHeight { get; }

    /// <summary>
    /// Per-component horizontal replication applied to transform output to reach the full sampling grid.
    /// </summary>
    public int[] ComponentReplicationH { get; }

    /// <summary>
    /// Per-component vertical replication applied to transform output to reach the full sampling grid.
    /// </summary>
    public int[] ComponentReplicationV { get; }

    /// <summary>
    /// True when at least one component has a smaller sampling factor than <see cref="HMax"/>/<see cref="VMax"/> and must be upsampled.
    /// </summary>
    public bool NeedsUpsampling { get; }

    /// <summary>
    /// Reports whether <paramref name="mcuRow"/> covers a region of interest and is reconstructed.
    /// </summary>
    /// <param name="mcuRow">Zero-based MCU row index.</param>
    public bool IsMcuRowReconstructed(int mcuRow) => _reconstructedMcuRows == null || _reconstructedMcuRows[mcuRow];

    /// <summary>
    /// Returns the largest transform size (a power of two, at most 8) that divides <paramref name="footprint"/>,
    /// leaving the remainder to be covered by replication.
    /// </summary>
    private static int LargestTransformSize(int footprint)
    {
        int size = DctSize;
        while (size > 1 && footprint % size != 0)
        {
            size >>= 1;
        }

        return size;
    }

    /// <summary>
    /// Spans the regions with a single MCU column range. MCU columns follow one another in the
    /// entropy stream, so a band covers everything between the outermost regions rather than
    /// only the columns a region lands on.
    /// </summary>
    private static void ComputeReconstructedColumns(
        IReadOnlyList<JpgRectangle> regionsOfInterest,
        int mcuWidth,
        int mcuColumns,
        out int reconstructedColumnStart,
        out int reconstructedColumns)
    {
        int firstColumn = mcuColumns - 1;
        int lastColumn = 0;

        for (int index = 0; index < regionsOfInterest.Count; index++)
        {
            JpgRectangle region = regionsOfInterest[index];
            int regionFirstColumn = ClampIndex(region.X / mcuWidth, mcuColumns);
            int regionLastColumn = ClampIndex((region.Right - 1) / mcuWidth, mcuColumns);

            if (regionFirstColumn < firstColumn)
            {
                firstColumn = regionFirstColumn;
            }

            if (regionLastColumn > lastColumn)
            {
                lastColumn = regionLastColumn;
            }
        }

        reconstructedColumnStart = firstColumn;
        reconstructedColumns = lastColumn - firstColumn + 1;
    }

    /// <summary>
    /// Marks every MCU row the regions reach.
    /// </summary>
    private static bool[] ComputeReconstructedRows(IReadOnlyList<JpgRectangle> regionsOfInterest, int mcuHeight, int mcuRows)
    {
        var reconstructedMcuRows = new bool[mcuRows];

        for (int index = 0; index < regionsOfInterest.Count; index++)
        {
            JpgRectangle region = regionsOfInterest[index];
            int firstRow = ClampIndex(region.Y / mcuHeight, mcuRows);
            int lastRow = ClampIndex((region.Bottom - 1) / mcuHeight, mcuRows);

            for (int mcuRow = firstRow; mcuRow <= lastRow; mcuRow++)
            {
                reconstructedMcuRows[mcuRow] = true;
            }
        }

        return reconstructedMcuRows;
    }

    private static int ClampIndex(int index, int count) => Math.Max(0, Math.Min(index, count - 1));
}
