using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using PdfRender.Imaging.Jpg.Model;

namespace PdfRender.Imaging.Jpg.Decoding;

/// <summary>
/// Performs block-level upsampling of component data from native sampling factors to the decoder's maximum sampling grid.
/// Geometry and sampling factors are obtained from <see cref="JpgDecodingParameters"/>.
/// Component specific scaling factors are precomputed into <see cref="ScalingInfo"/> records to avoid per-band header traversal.
/// </summary>
internal sealed class JpgUpsampler
{
    private readonly JpgDecodingParameters _parameters;
    private readonly JpgHeader _header;
    private readonly ScalingInfo[] _scalingInfos;
    private readonly int _fullBlocksPerMcu; // HMax * VMax cached

    private readonly struct ScalingInfo
    {
        public ScalingInfo(int hFactor, int vFactor, int horizontalScale, int verticalScale)
        {
            HFactor = hFactor;
            VFactor = vFactor;
            HorizontalScale = horizontalScale;
            VerticalScale = verticalScale;
            BlocksPerMcu = hFactor * vFactor;
        }

        public int HFactor { get; }
        public int VFactor { get; }
        public int HorizontalScale { get; }
        public int VerticalScale { get; }
        public int BlocksPerMcu { get; }
    }

    public JpgUpsampler(JpgDecodingParameters parameters, JpgHeader header)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        _parameters = parameters;
        _header = header;
        _fullBlocksPerMcu = parameters.HMax * parameters.VMax;

        int componentCount = header.ComponentCount;
        _scalingInfos = new ScalingInfo[componentCount];
        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            JpgComponent component = header.Components[componentIndex];
            int hFactor = component.HorizontalSamplingFactor;
            int vFactor = component.VerticalSamplingFactor;
            if (hFactor <= 0 || vFactor <= 0 || parameters.HMax % hFactor != 0 || parameters.VMax % vFactor != 0)
            {
                throw new ArgumentException("Invalid sampling factors relative to max sampling factors.");
            }
            int horizontalScale = parameters.HMax / hFactor;
            int verticalScale = parameters.VMax / vFactor;
            _scalingInfos[componentIndex] = new ScalingInfo(hFactor, vFactor, horizontalScale, verticalScale);
        }
    }

    /// <summary>
    /// Upsample all components for the current band into the supplied destination full-resolution block arrays.
    /// </summary>
    /// <param name="sourceBandBlocks">Native sampling blocks per component (size: TotalBlocksPerBand[component]).</param>
    /// <param name="destFullResBlocks">Destination arrays (per component) sized to (McuColumns * HMax * VMax) blocks.</param>
    public void UpsampleBand(Block8x8F[][] sourceBandBlocks, Block8x8F[][] destFullResBlocks)
    {
        if (sourceBandBlocks == null)
        {
            throw new ArgumentNullException(nameof(sourceBandBlocks));
        }
        if (destFullResBlocks == null)
        {
            throw new ArgumentNullException(nameof(destFullResBlocks));
        }
        if (sourceBandBlocks.Length != _header.ComponentCount || destFullResBlocks.Length != _header.ComponentCount)
        {
            throw new ArgumentException("Component array length mismatch with header component count.");
        }

        for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
        {
            ref ScalingInfo info = ref _scalingInfos[componentIndex];
            Block8x8F[] destBlocks = destFullResBlocks[componentIndex];
            Block8x8F[] sourceBlocks = sourceBandBlocks[componentIndex];

            if (info.HorizontalScale == 1 && info.VerticalScale == 1)
            {
                FastCopy(sourceBlocks, destBlocks, in info);
            }
            else
            {
                GenericUpsampleComponent(sourceBlocks, destBlocks, in info);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FastCopy(Block8x8F[] sourceBlocks, Block8x8F[] destBlocks, in ScalingInfo info)
    {
        int blocksPerMcu = info.BlocksPerMcu;
        for (int mcuColumnIndex = 0; mcuColumnIndex < _parameters.McuColumns; mcuColumnIndex++)
        {
            int srcBase = mcuColumnIndex * blocksPerMcu;
            int dstBase = mcuColumnIndex * blocksPerMcu;
            for (int blockOffset = 0; blockOffset < blocksPerMcu; blockOffset++)
            {
                destBlocks[dstBase + blockOffset] = sourceBlocks[srcBase + blockOffset];
            }
        }
    }

    private void GenericUpsampleComponent(Block8x8F[] sourceBlocks, Block8x8F[] destBlocks, in ScalingInfo info)
    {
        for (int mcuColumnIndex = 0; mcuColumnIndex < _parameters.McuColumns; mcuColumnIndex++)
        {
            int fullBase = mcuColumnIndex * _fullBlocksPerMcu;
            for (int fullBlockRow = 0; fullBlockRow < _parameters.VMax; fullBlockRow++)
            {
                for (int fullBlockCol = 0; fullBlockCol < _parameters.HMax; fullBlockCol++)
                {
                    int destIndex = fullBase + fullBlockRow * _parameters.HMax + fullBlockCol;
                    UpsampleBlock(sourceBlocks, mcuColumnIndex, fullBlockRow, fullBlockCol, in info, ref destBlocks[destIndex]);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpsampleBlock(
        Block8x8F[] sourceBlocks,
        int mcuColumnIndex,
        int fullBlockRow,
        int fullBlockCol,
        in ScalingInfo info,
        ref Block8x8F dest)
    {
        int nativeBlocksPerMcu = info.BlocksPerMcu;
        int sourceBlockRow = fullBlockRow / info.VerticalScale;
        int sourceBlockCol = fullBlockCol / info.HorizontalScale;
        int localSourceIndex = sourceBlockRow * info.HFactor + sourceBlockCol;
        int globalSourceIndex = mcuColumnIndex * nativeBlocksPerMcu + localSourceIndex;
        ref Block8x8F sourceBlock = ref sourceBlocks[globalSourceIndex];

        if (info.HorizontalScale == 1 && info.VerticalScale == 1)
        {
            dest = sourceBlock;
            return;
        }

        int hScale = info.HorizontalScale;
        int vScale = info.VerticalScale;

        // Optimized and correct 2x paths using quarter selection inside the source block.
        // For subsampled chroma (e.g. 4:2:0) a single native block represents a 2x or 4x pixel area.
        // Each destination block corresponds to a quadrant (when both scales are 2) or half region (single 2x scale).
        if (hScale == 2 && vScale == 1)
        {
            // Horizontal only upsample. Choose the left/right 4-column quarter then expand 4 -> 8 by duplicating lanes.
            int quarterColBase = (fullBlockCol & 1) * 4; // 0 for left block, 4 for right block.
            for (int rowIndex = 0; rowIndex < 8; rowIndex++)
            {
                int vecBaseSrc = rowIndex * 2;
                // Select vector containing the 4 source samples for this dest block half.
                Vector4 quarter = sourceBlock.GetVector(vecBaseSrc + (quarterColBase == 0 ? 0 : 1));
                // Expand 4 samples (a b c d) -> 8 samples (a a b b c c d d).
                Vector4 leftExpanded = new Vector4(quarter.X, quarter.X, quarter.Y, quarter.Y);
                Vector4 rightExpanded = new Vector4(quarter.Z, quarter.Z, quarter.W, quarter.W);
                int destVecBase = rowIndex * 2;
                dest.SetVector(destVecBase + 0, leftExpanded);
                dest.SetVector(destVecBase + 1, rightExpanded);
            }
            return;
        }
        if (hScale == 1 && vScale == 2)
        {
            // Vertical only upsample. Choose the top/bottom 4-row quarter then duplicate each row vertically.
            int quarterRowBase = (fullBlockRow & 1) * 4; // 0 for top block, 4 for bottom block.
            for (int destRow = 0; destRow < 8; destRow++)
            {
                int srcRow = quarterRowBase + (destRow >> 1); // Each source row maps to two dest rows.
                int srcVecBase = srcRow * 2;
                Vector4 left = sourceBlock.GetVector(srcVecBase + 0);
                Vector4 right = sourceBlock.GetVector(srcVecBase + 1);
                int destVecBase = destRow * 2;
                dest.SetVector(destVecBase + 0, left);
                dest.SetVector(destVecBase + 1, right);
            }
            return;
        }
        if (hScale == 2 && vScale == 2)
        {
            // Both horizontal and vertical upsample. Select 4x4 quarter then expand each 4x4 sample into 8x8 via 2x2 replication.
            int quarterRowBase = (fullBlockRow & 1) * 4; // 0 or 4.
            int quarterColBase = (fullBlockCol & 1) * 4; // 0 or 4.
            bool useLeft = quarterColBase == 0;
            for (int destRow = 0; destRow < 8; destRow++)
            {
                int srcRow = quarterRowBase + (destRow >> 1);
                int srcVecBase = srcRow * 2;
                Vector4 quarterVector = sourceBlock.GetVector(srcVecBase + (useLeft ? 0 : 1));
                Vector4 leftExpanded = new Vector4(quarterVector.X, quarterVector.X, quarterVector.Y, quarterVector.Y);
                Vector4 rightExpanded = new Vector4(quarterVector.Z, quarterVector.Z, quarterVector.W, quarterVector.W);
                int destVecBase = destRow * 2;
                dest.SetVector(destVecBase + 0, leftExpanded);
                dest.SetVector(destVecBase + 1, rightExpanded);
            }
            return;
        }

        // Fallback generic path (scales other than 1 or 2). Per-pixel replication using scalar indices.
        for (int rowInDest = 0; rowInDest < 8; rowInDest++)
        {
            int fullRow = fullBlockRow * 8 + rowInDest;
            int sourceRow = fullRow / vScale;
            for (int colInDest = 0; colInDest < 8; colInDest++)
            {
                int fullCol = fullBlockCol * 8 + colInDest;
                int sourceCol = fullCol / hScale;
                int sourceIndex = sourceRow * 8 + sourceCol;
                int destIndex = rowInDest * 8 + colInDest;
                dest[destIndex] = sourceBlock[sourceIndex];
            }
        }
    }
}
