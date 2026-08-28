using System;
using System.Runtime.CompilerServices;

using PdfPixel.Jpg.Model;

namespace PdfPixel.Jpg.Decoding;

internal sealed class JpgBandPacker
{
    private const int BlockRowStride = 8;

    private readonly JpgHeader _header;
    private readonly JpgDecodingParameters _parameters;

    public JpgBandPacker(JpgHeader header, JpgDecodingParameters parameters)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        _header = header;
        _parameters = parameters;
    }

    /// <summary>
    /// Writes one row of the current band into <paramref name="destination"/>, interleaving the components.
    /// </summary>
    /// <param name="fullResBlocks">Per-component blocks of the current band.</param>
    /// <param name="bandRow">Row within the band, in output samples.</param>
    /// <param name="destination">Row to fill; the caller owns it.</param>
    public void PackRow(Block8x8F[][] fullResBlocks, int bandRow, in Span<byte> destination)
    {
        if (fullResBlocks == null)
        {
            throw new ArgumentNullException(nameof(fullResBlocks));
        }

        switch (_header.ComponentCount)
        {
            case 1:
                {
                    PackGrayRow(fullResBlocks, bandRow, destination);
                    break;
                }
            case 3:
                {
                    PackRgbRow(fullResBlocks, bandRow, destination);
                    break;
                }
            case 4:
                {
                    PackCmykRow(fullResBlocks, bandRow, destination);
                    break;
                }
            default:
                {
                    PackInterleavedRow(fullResBlocks, bandRow, destination);
                    break;
                }
        }
    }

    private void PackGrayRow(Block8x8F[][] grayBlocks, int bandRow, in Span<byte> destination)
    {
        Block8x8F[] yBlocks = grayBlocks[0];
        RowGeometry geometry = new(_parameters, bandRow);

        for (int mcuColumnIndex = 0; mcuColumnIndex < _parameters.ReconstructedMcuColumns; mcuColumnIndex++)
        {
            if (!geometry.TryStartMcuColumn(mcuColumnIndex, out int mcuXBase, out int effectiveColumnWidth, out int blockBase))
            {
                break;
            }

            for (int blockColumn = 0; blockColumn < geometry.BlocksPerRow; blockColumn++)
            {
                int blockXBase = blockColumn * geometry.BlockSize;
                if (blockXBase >= effectiveColumnWidth)
                {
                    break;
                }

                int copyPixels = geometry.GetCopyPixels(effectiveColumnWidth, blockXBase);
                ref float yRow = ref geometry.GetSampleRow(yBlocks, blockBase + blockColumn);
                ref byte destRef = ref destination[mcuXBase + blockXBase];

                for (int px = 0; px < copyPixels; px++)
                {
                    destRef = (byte)Unsafe.Add(ref yRow, px);
                    destRef = ref Unsafe.Add(ref destRef, 1);
                }
            }
        }
    }

    private void PackRgbRow(Block8x8F[][] rgbBlocks, int bandRow, in Span<byte> destination)
    {
        Block8x8F[] rBlocks = rgbBlocks[0];
        Block8x8F[] gBlocks = rgbBlocks[1];
        Block8x8F[] bBlocks = rgbBlocks[2];
        RowGeometry geometry = new(_parameters, bandRow);

        for (int mcuColumnIndex = 0; mcuColumnIndex < _parameters.ReconstructedMcuColumns; mcuColumnIndex++)
        {
            if (!geometry.TryStartMcuColumn(mcuColumnIndex, out int mcuXBase, out int effectiveColumnWidth, out int blockBase))
            {
                break;
            }

            for (int blockColumn = 0; blockColumn < geometry.BlocksPerRow; blockColumn++)
            {
                int blockXBase = blockColumn * geometry.BlockSize;
                if (blockXBase >= effectiveColumnWidth)
                {
                    break;
                }

                int copyPixels = geometry.GetCopyPixels(effectiveColumnWidth, blockXBase);
                int blockIndex = blockBase + blockColumn;
                ref float rRow = ref geometry.GetSampleRow(rBlocks, blockIndex);
                ref float gRow = ref geometry.GetSampleRow(gBlocks, blockIndex);
                ref float bRow = ref geometry.GetSampleRow(bBlocks, blockIndex);
                ref byte destRef = ref destination[(mcuXBase + blockXBase) * 3];

                for (int px = 0; px < copyPixels; px++)
                {
                    destRef = (byte)Unsafe.Add(ref rRow, px);
                    Unsafe.Add(ref destRef, 1) = (byte)Unsafe.Add(ref gRow, px);
                    Unsafe.Add(ref destRef, 2) = (byte)Unsafe.Add(ref bRow, px);
                    destRef = ref Unsafe.Add(ref destRef, 3);
                }
            }
        }
    }

    private void PackCmykRow(Block8x8F[][] cmykBlocks, int bandRow, in Span<byte> destination)
    {
        Block8x8F[] cBlocks = cmykBlocks[0];
        Block8x8F[] mBlocks = cmykBlocks[1];
        Block8x8F[] yBlocks = cmykBlocks[2];
        Block8x8F[] kBlocks = cmykBlocks[3];
        RowGeometry geometry = new(_parameters, bandRow);

        for (int mcuColumnIndex = 0; mcuColumnIndex < _parameters.ReconstructedMcuColumns; mcuColumnIndex++)
        {
            if (!geometry.TryStartMcuColumn(mcuColumnIndex, out int mcuXBase, out int effectiveColumnWidth, out int blockBase))
            {
                break;
            }

            for (int blockColumn = 0; blockColumn < geometry.BlocksPerRow; blockColumn++)
            {
                int blockXBase = blockColumn * geometry.BlockSize;
                if (blockXBase >= effectiveColumnWidth)
                {
                    break;
                }

                int copyPixels = geometry.GetCopyPixels(effectiveColumnWidth, blockXBase);
                int blockIndex = blockBase + blockColumn;
                ref float cRow = ref geometry.GetSampleRow(cBlocks, blockIndex);
                ref float mRow = ref geometry.GetSampleRow(mBlocks, blockIndex);
                ref float yRow = ref geometry.GetSampleRow(yBlocks, blockIndex);
                ref float kRow = ref geometry.GetSampleRow(kBlocks, blockIndex);
                ref byte destRef = ref destination[(mcuXBase + blockXBase) * 4];

                for (int px = 0; px < copyPixels; px++)
                {
                    destRef = (byte)Unsafe.Add(ref cRow, px);
                    Unsafe.Add(ref destRef, 1) = (byte)Unsafe.Add(ref mRow, px);
                    Unsafe.Add(ref destRef, 2) = (byte)Unsafe.Add(ref yRow, px);
                    Unsafe.Add(ref destRef, 3) = (byte)Unsafe.Add(ref kRow, px);
                    destRef = ref Unsafe.Add(ref destRef, 4);
                }
            }
        }
    }

    /// <summary>
    /// Packer for any component count, writing one component plane at a time into the interleaved
    /// destination. Used for images whose component count has no dedicated fast path.
    /// </summary>
    private void PackInterleavedRow(Block8x8F[][] fullResBlocks, int bandRow, in Span<byte> destination)
    {
        int componentCount = _header.ComponentCount;
        RowGeometry geometry = new(_parameters, bandRow);

        for (int mcuColumnIndex = 0; mcuColumnIndex < _parameters.ReconstructedMcuColumns; mcuColumnIndex++)
        {
            if (!geometry.TryStartMcuColumn(mcuColumnIndex, out int mcuXBase, out int effectiveColumnWidth, out int blockBase))
            {
                break;
            }

            for (int blockColumn = 0; blockColumn < geometry.BlocksPerRow; blockColumn++)
            {
                int blockXBase = blockColumn * geometry.BlockSize;
                if (blockXBase >= effectiveColumnWidth)
                {
                    break;
                }

                int copyPixels = geometry.GetCopyPixels(effectiveColumnWidth, blockXBase);
                int blockIndex = blockBase + blockColumn;
                int destPixelOffset = (mcuXBase + blockXBase) * componentCount;

                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    ref float sourceRow = ref geometry.GetSampleRow(fullResBlocks[componentIndex], blockIndex);
                    ref byte destRef = ref destination[destPixelOffset + componentIndex];

                    for (int px = 0; px < copyPixels; px++)
                    {
                        destRef = (byte)Unsafe.Add(ref sourceRow, px);
                        destRef = ref Unsafe.Add(ref destRef, componentCount);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Locates the blocks and samples one band row draws from, shared by the per-component-count packers.
    /// </summary>
    private readonly struct RowGeometry
    {
        private readonly int _outputMcuWidth;
        private readonly int _outputWidth;
        private readonly int _fullBlocksPerMcu;
        private readonly int _mcuColumnStart;
        private readonly int _blockRowBase;
        private readonly int _sampleRowOffset;

        public RowGeometry(JpgDecodingParameters parameters, int bandRow)
        {
            BlockSize = parameters.BlockSize;
            BlocksPerRow = parameters.HMax;
            _outputMcuWidth = parameters.OutputMcuWidth;
            _outputWidth = parameters.OutputWidth;
            _fullBlocksPerMcu = parameters.HMax * parameters.VMax;
            _mcuColumnStart = parameters.ReconstructedMcuColumnStart;

            int blockRow = bandRow / BlockSize;
            _blockRowBase = blockRow * parameters.HMax;
            _sampleRowOffset = (bandRow - (blockRow * BlockSize)) * BlockRowStride;
        }

        public int BlockSize { get; }

        public int BlocksPerRow { get; }

        /// <summary>
        /// Positions this row inside one MCU column. Returns false once the column starts past the image.
        /// </summary>
        /// <param name="mcuColumnIndex">MCU column to start, counted from the first column the band holds.</param>
        /// <param name="mcuXBase">First output sample the column covers.</param>
        /// <param name="effectiveColumnWidth">Samples of the column that fall inside the image.</param>
        /// <param name="blockBase">Index of the column's first block on this block row.</param>
        public bool TryStartMcuColumn(int mcuColumnIndex, out int mcuXBase, out int effectiveColumnWidth, out int blockBase)
        {
            mcuXBase = (_mcuColumnStart + mcuColumnIndex) * _outputMcuWidth;
            blockBase = (mcuColumnIndex * _fullBlocksPerMcu) + _blockRowBase;

            int remainingColumnPixels = _outputWidth - mcuXBase;
            effectiveColumnWidth = (remainingColumnPixels < _outputMcuWidth) ? remainingColumnPixels : _outputMcuWidth;

            return remainingColumnPixels > 0;
        }

        /// <summary>
        /// Samples one block contributes to this row, clipped to the image.
        /// </summary>
        /// <param name="effectiveColumnWidth">Samples of the MCU column that fall inside the image.</param>
        /// <param name="blockXBase">First sample the block covers within its MCU column.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCopyPixels(int effectiveColumnWidth, int blockXBase)
        {
            int copyPixels = effectiveColumnWidth - blockXBase;
            return (copyPixels > BlockSize) ? BlockSize : copyPixels;
        }

        /// <summary>
        /// Reference to the first sample this row reads from the given block.
        /// </summary>
        /// <param name="blocks">Blocks of one component.</param>
        /// <param name="blockIndex">Block to read.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref float GetSampleRow(Block8x8F[] blocks, int blockIndex)
        {
            ref float blockBase = ref Unsafe.As<Block8x8F, float>(ref blocks[blockIndex]);
            return ref Unsafe.Add(ref blockBase, _sampleRowOffset);
        }
    }
}
