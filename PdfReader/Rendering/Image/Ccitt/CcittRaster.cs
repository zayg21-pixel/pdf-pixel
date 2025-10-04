using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PdfReader.Rendering.Image.Ccitt
{
    /// <summary>
    /// Utility methods for validating runs and writing CCITT decoded runs into a packed 1-bit buffer.
    /// Bit semantics: if BlackIs1 is true then bit 1 encodes black pixels (white = 0). Otherwise bit 0 encodes black (white = 1).
    /// The first run in <paramref name="runs"/> is white (may be zero length) per CCITT specification.
    /// </summary>
    internal static class CcittRaster
    {
        /// <summary>
        /// Create a packed 1-bit-per-pixel buffer initialized to the white background value according to <paramref name="blackIs1"/>.
        /// </summary>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="blackIs1">If true, bit value 1 represents black and 0 represents white; otherwise bit value 0 represents black and 1 represents white.</param>
        /// <param name="byteCount">Allocated bytes count.</param>
        /// <returns>Initialized packed bit buffer of length ((width + 7) / 8) * height.</returns>
        public static unsafe IntPtr AllocateBuffer(int width, int height, bool blackIs1, out int byteCount)
        {
            int rowBytes = (width + 7) / 8;
            int total = rowBytes * height;
            IntPtr unmanaged = Marshal.AllocHGlobal(total);
            byte* buffer = (byte*)unmanaged.ToPointer();

            // Determine background (white) bit value
            int whiteBit = blackIs1 ? 0 : 1;
            if (whiteBit == 1)
            {
                // Fill all bits to 1 (white) when whiteBit=1
                for (int i = 0; i < total; i++)
                {
                    buffer[i] = 0xFF;
                }
            }
            // else already zeroed = all white when whiteBit == 0
            byteCount = total;
            return unmanaged;
        }

        /// <summary>
        /// Validate that the sum of run lengths matches the expected width.
        /// </summary>
        public static void ValidateRunLengths(List<int> runs, int expectedWidth, int rowIndex, string decoderName)
        {
            int total = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                total += runs[i];
            }
            if (total != expectedWidth)
            {
                throw new System.InvalidOperationException(decoderName + " decode error: row length mismatch row=" + rowIndex + " got=" + total + " expected=" + expectedWidth + ".");
            }
        }

        /// <summary>
        /// Rasterize run-length data into the packed bit buffer. First run is white (may be zero) and colors alternate.
        /// Background was pre-initialized to white; only the black runs are written by flipping bits when necessary.
        /// </summary>
        /// <param name="buffer">Packed 1-bit buffer.</param>
        /// <param name="runs">Run lengths (first white) alternating white/black.</param>
        /// <param name="rowIndex">Row index being written.</param>
        /// <param name="width">Row width in pixels.</param>
        /// <param name="blackIs1">Bit polarity (1=black when true).</param>
        public static void RasterizeRuns(Span<byte> buffer, List<int> runs, int rowIndex, int width, bool blackIs1)
        {
            int rowBytes = (width + 7) / 8;
            int rowBase = rowIndex * rowBytes;
            int x = 0;
            bool isBlack = false; // first run white
            int blackBit = blackIs1 ? 1 : 0;
            int whiteBit = 1 - blackBit;

            for (int r = 0; r < runs.Count; r++)
            {
                int runLength = runs[r];
                if (runLength > 0 && isBlack)
                {
                    WriteBlackRun(buffer, rowBase, x, runLength, width, blackBit, whiteBit);
                }
                x += runLength;
                isBlack = !isBlack;
            }
        }

        /// <summary>
        /// Build reference change list from run lengths for subsequent 2D line processing.
        /// </summary>
        public static void BuildReferenceChangeList(List<int> runs, int width, List<int> nextReferenceChanges)
        {
            nextReferenceChanges.Clear();
            if (runs.Count > 0 && runs[0] == 0)
            {
                nextReferenceChanges.Add(0);
            }
            int accumulator = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                accumulator += runs[i];
                if (accumulator > 0 && accumulator < width)
                {
                    nextReferenceChanges.Add(accumulator);
                }
            }
            if (nextReferenceChanges.Count == 0 || nextReferenceChanges[nextReferenceChanges.Count - 1] != width)
            {
                nextReferenceChanges.Add(width);
            }
        }

        private static void WriteBlackRun(Span<byte> buffer, int rowBase, int startX, int length, int width, int blackBit, int whiteBit)
        {
            if (length <= 0)
            {
                return;
            }
            int endX = startX + length;
            int startByte = startX >> 3;
            int endByte = (endX - 1) >> 3;
            int rowOffsetStart = rowBase + startByte;
            int bitStart = startX & 7;
            int bitEnd = (endX - 1) & 7;
            // When whiteBit ==1 and blackBit==0 we clear bits to 0 for black; when whiteBit==0 and blackBit==1 we set bits.
            bool setBits = blackBit == 1;

            if (startByte == endByte)
            {
                int mask = ((0xFF >> bitStart) & (0xFF << (7 - bitEnd)));
                if (setBits)
                {
                    buffer[rowOffsetStart] |= (byte)mask;
                }
                else
                {
                    buffer[rowOffsetStart] &= (byte)~mask;
                }
                return;
            }

            // First partial byte
            if (bitStart != 0)
            {
                int firstMask = 0xFF >> bitStart;
                if (setBits)
                {
                    buffer[rowOffsetStart] |= (byte)firstMask;
                }
                else
                {
                    buffer[rowOffsetStart] &= (byte)~firstMask;
                }
                startByte++;
                rowOffsetStart++;
            }

            // Full bytes
            for (int b = startByte; b < endByte; b++)
            {
                buffer[rowBase + b] = setBits ? (byte)0xFF : (byte)0x00;
            }

            // Last partial byte
            int lastMask = 0xFF << (7 - bitEnd);
            int lastIndex = rowBase + endByte;
            if (setBits)
            {
                buffer[lastIndex] |= (byte)lastMask;
            }
            else
            {
                buffer[lastIndex] &= (byte)~lastMask;
            }
        }
    }
}
