using System;
using System.Runtime.CompilerServices;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    internal sealed class JpgBandPacker
    {
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

        public void Pack(Block8x8F[][] fullResBlocks, int bandRows, byte[] destination)
        {
            if (fullResBlocks == null)
            {
                throw new ArgumentNullException(nameof(fullResBlocks));
            }
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
            if (bandRows <= 0)
            {
                return;
            }

            switch (_header.ComponentCount)
            {
                case 1:
                    PackGray(fullResBlocks, bandRows, destination);
                    break;
                case 3:
                    PackRgb(fullResBlocks, bandRows, destination);
                    break;
                case 4:
                    PackCmyk(fullResBlocks, bandRows, destination);
                    break;
                default:
                    throw new NotSupportedException("Unsupported component count: " + _header.ComponentCount);
            }
        }

        /// <summary>
        /// High-performance grayscale packer using linear block traversal (same strategy as RGB/CMYK variants).
        /// </summary>
        private void PackGray(Block8x8F[][] grayBlocks, int bandRows, byte[] destination)
        {
            Block8x8F[] yBlocks = grayBlocks[0];
            int hMax = _parameters.HMax;
            int vMax = _parameters.VMax;
            int fullBlocksPerMcu = hMax * vMax;
            int mcuWidth = _parameters.McuWidth;
            int outputStride = _parameters.OutputStride; // equals image width for grayscale
            int imageWidth = _header.Width;
            int blocksPerBand = _parameters.McuColumns * fullBlocksPerMcu;

            for (int blockLinearIndex = 0; blockLinearIndex < blocksPerBand; blockLinearIndex++)
            {
                int mcuColumnIndex = blockLinearIndex / fullBlocksPerMcu;
                int blockInColumn = blockLinearIndex - mcuColumnIndex * fullBlocksPerMcu;
                int blockRow = blockInColumn / hMax;
                int blockCol = blockInColumn - blockRow * hMax;

                int xBase = mcuColumnIndex * mcuWidth;
                int blockXBase = blockCol * 8;
                int remainingColumnPixels = imageWidth - xBase;
                if (remainingColumnPixels <= 0)
                {
                    break;
                }
                int effectiveColumnWidth = remainingColumnPixels < mcuWidth ? remainingColumnPixels : mcuWidth;
                if (blockXBase >= effectiveColumnWidth)
                {
                    continue;
                }
                int copyPixels = effectiveColumnWidth - blockXBase;
                if (copyPixels > 8)
                {
                    copyPixels = 8;
                }

                int blockYBase = blockRow * 8;
                if (blockYBase >= bandRows)
                {
                    continue;
                }
                int maxRowsInBlock = bandRows - blockYBase;
                if (maxRowsInBlock > 8)
                {
                    maxRowsInBlock = 8;
                }

                ref float yBlockBase = ref Unsafe.As<Block8x8F, float>(ref yBlocks[blockLinearIndex]);
                for (int localRow = 0; localRow < maxRowsInBlock; localRow++)
                {
                    int rowFloatOffset = localRow * 8;
                    ref float yRow = ref Unsafe.Add(ref yBlockBase, rowFloatOffset);
                    int destRowOffset = (blockYBase + localRow) * outputStride;
                    int destPixelOffset = destRowOffset + xBase + blockXBase;
                    ref byte destRef = ref destination[destPixelOffset];
                    for (int px = 0; px < copyPixels; px++)
                    {
                        byte y = (byte)Unsafe.Add(ref yRow, px);
                        destRef = y;
                        destRef = ref Unsafe.Add(ref destRef, 1);
                    }
                }
            }
        }

        /// <summary>
        /// High-performance RGB packer using linear block traversal. Each block (8x8) is visited once and its
        /// valid rows/columns are copied directly to the destination. ~20% faster than row-major variant in testing.
        /// </summary>
        private void PackRgb(Block8x8F[][] rgbBlocks, int bandRows, byte[] destination)
        {
            Block8x8F[] rBlocks = rgbBlocks[0];
            Block8x8F[] gBlocks = rgbBlocks[1];
            Block8x8F[] bBlocks = rgbBlocks[2];

            int hMax = _parameters.HMax;
            int vMax = _parameters.VMax;
            int fullBlocksPerMcu = hMax * vMax;
            int mcuWidth = _parameters.McuWidth;
            int outputStride = _parameters.OutputStride;
            int imageWidth = _header.Width;
            int blocksPerBand = _parameters.McuColumns * fullBlocksPerMcu;

            for (int blockLinearIndex = 0; blockLinearIndex < blocksPerBand; blockLinearIndex++)
            {
                int mcuColumnIndex = blockLinearIndex / fullBlocksPerMcu;
                int blockInColumn = blockLinearIndex - mcuColumnIndex * fullBlocksPerMcu;
                int blockRow = blockInColumn / hMax;
                int blockCol = blockInColumn - blockRow * hMax;

                int xBase = mcuColumnIndex * mcuWidth;
                int blockXBase = blockCol * 8;
                int remainingColumnPixels = imageWidth - xBase;
                if (remainingColumnPixels <= 0)
                {
                    break;
                }
                int effectiveColumnWidth = remainingColumnPixels < mcuWidth ? remainingColumnPixels : mcuWidth;
                if (blockXBase >= effectiveColumnWidth)
                {
                    continue;
                }
                int copyPixels = effectiveColumnWidth - blockXBase;
                if (copyPixels > 8)
                {
                    copyPixels = 8;
                }

                int blockYBase = blockRow * 8;
                if (blockYBase >= bandRows)
                {
                    continue;
                }
                int maxRowsInBlock = bandRows - blockYBase;
                if (maxRowsInBlock > 8)
                {
                    maxRowsInBlock = 8;
                }

                ref float rBlockBase = ref Unsafe.As<Block8x8F, float>(ref rBlocks[blockLinearIndex]);
                ref float gBlockBase = ref Unsafe.As<Block8x8F, float>(ref gBlocks[blockLinearIndex]);
                ref float bBlockBase = ref Unsafe.As<Block8x8F, float>(ref bBlocks[blockLinearIndex]);

                for (int localRow = 0; localRow < maxRowsInBlock; localRow++)
                {
                    int rowFloatOffset = localRow * 8;
                    ref float rRow = ref Unsafe.Add(ref rBlockBase, rowFloatOffset);
                    ref float gRow = ref Unsafe.Add(ref gBlockBase, rowFloatOffset);
                    ref float bRow = ref Unsafe.Add(ref bBlockBase, rowFloatOffset);
                    int destRowOffset = (blockYBase + localRow) * outputStride;
                    int destPixelOffset = destRowOffset + (xBase + blockXBase) * 3;
                    ref byte destRef = ref destination[destPixelOffset];
                    for (int px = 0; px < copyPixels; px++)
                    {
                        byte r = (byte)Unsafe.Add(ref rRow, px);
                        byte g = (byte)Unsafe.Add(ref gRow, px);
                        byte b = (byte)Unsafe.Add(ref bRow, px);
                        destRef = r;
                        Unsafe.Add(ref destRef, 1) = g;
                        Unsafe.Add(ref destRef, 2) = b;
                        destRef = ref Unsafe.Add(ref destRef, 3);
                    }
                }
            }
        }

        /// <summary>
        /// High-performance CMYK packer using the same linear block traversal strategy.
        /// </summary>
        private void PackCmyk(Block8x8F[][] cmykBlocks, int bandRows, byte[] destination)
        {
            Block8x8F[] cBlocks = cmykBlocks[0];
            Block8x8F[] mBlocks = cmykBlocks[1];
            Block8x8F[] yBlocks = cmykBlocks[2];
            Block8x8F[] kBlocks = cmykBlocks[3];

            int hMax = _parameters.HMax;
            int vMax = _parameters.VMax;
            int fullBlocksPerMcu = hMax * vMax;
            int mcuWidth = _parameters.McuWidth;
            int outputStride = _parameters.OutputStride;
            int imageWidth = _header.Width;
            int blocksPerBand = _parameters.McuColumns * fullBlocksPerMcu;

            for (int blockLinearIndex = 0; blockLinearIndex < blocksPerBand; blockLinearIndex++)
            {
                int mcuColumnIndex = blockLinearIndex / fullBlocksPerMcu;
                int blockInColumn = blockLinearIndex - mcuColumnIndex * fullBlocksPerMcu;
                int blockRow = blockInColumn / hMax;
                int blockCol = blockInColumn - blockRow * hMax;

                int xBase = mcuColumnIndex * mcuWidth;
                int blockXBase = blockCol * 8;
                int remainingColumnPixels = imageWidth - xBase;
                if (remainingColumnPixels <= 0)
                {
                    break;
                }
                int effectiveColumnWidth = remainingColumnPixels < mcuWidth ? remainingColumnPixels : mcuWidth;
                if (blockXBase >= effectiveColumnWidth)
                {
                    continue;
                }
                int copyPixels = effectiveColumnWidth - blockXBase;
                if (copyPixels > 8)
                {
                    copyPixels = 8;
                }

                int blockYBase = blockRow * 8;
                if (blockYBase >= bandRows)
                {
                    continue;
                }
                int maxRowsInBlock = bandRows - blockYBase;
                if (maxRowsInBlock > 8)
                {
                    maxRowsInBlock = 8;
                }

                ref float cBlockBase = ref Unsafe.As<Block8x8F, float>(ref cBlocks[blockLinearIndex]);
                ref float mBlockBase = ref Unsafe.As<Block8x8F, float>(ref mBlocks[blockLinearIndex]);
                ref float yBlockBase = ref Unsafe.As<Block8x8F, float>(ref yBlocks[blockLinearIndex]);
                ref float kBlockBase = ref Unsafe.As<Block8x8F, float>(ref kBlocks[blockLinearIndex]);

                for (int localRow = 0; localRow < maxRowsInBlock; localRow++)
                {
                    int rowFloatOffset = localRow * 8;
                    ref float cRow = ref Unsafe.Add(ref cBlockBase, rowFloatOffset);
                    ref float mRow = ref Unsafe.Add(ref mBlockBase, rowFloatOffset);
                    ref float yRow = ref Unsafe.Add(ref yBlockBase, rowFloatOffset);
                    ref float kRow = ref Unsafe.Add(ref kBlockBase, rowFloatOffset);
                    int destRowOffset = (blockYBase + localRow) * outputStride;
                    int destPixelOffset = destRowOffset + (xBase + blockXBase) * 4;
                    ref byte destRef = ref destination[destPixelOffset];
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
    }
}
