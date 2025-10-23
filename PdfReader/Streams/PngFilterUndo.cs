using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfReader.Streams
{
    /// <summary>
    /// Helper for undoing PNG predictor filters (types 0..4) with optional SIMD acceleration
    /// for Sub (1), Up (2) and Average (3) filters. Operates on buffers that include a left zero margin
    /// of bytesPerPixel bytes (to avoid bounds checks) and optionally a leading filter byte
    /// (caller handles layout). No allocations; purely in-place.
    /// </summary>
    internal static class PngFilterUndo
    {
        /// <summary>
        /// Applies PNG filter undo to a single row.
        /// </summary>
        /// <param name="filterType">PNG filter byte (0..4).</param>
        /// <param name="currentRow">Buffer containing row to be decoded in-place.</param>
        /// <param name="previousRow">Previous decoded row (same layout) or buffer of zeros for first row.</param>
        /// <param name="bytesPerPixel">Bytes per pixel (color components * bits per component rounded up to whole bytes).</param>
        /// <param name="rowDataOffset">Offset in buffers where actual pixel row data begins (after margin + filter byte).</param>
        /// <param name="rowDataLength">Number of pixel data bytes in the row.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void UndoPngFilter(byte filterType, byte[] currentRow, byte[] previousRow, int bytesPerPixel, int rowDataOffset, int rowDataLength)
        {
            Span<byte> currentRowSpan = currentRow;
            Span<byte> previousRowSpan = previousRow;

            byte* currentRowPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(currentRowSpan));
            byte* previousRowPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(previousRowSpan));

            switch (filterType)
            {
                case 0:
                    {
                        // None: nothing to do.
                        return;
                    }
                case 1:
                    {
                        // Sub: raw[i] += raw[i - bytesPerPixel]. Left dependency distance permits SIMD after first bytesPerPixel bytes.
                        ref byte rawRef = ref currentRow[rowDataOffset];
                        ref byte leftRef = ref currentRow[rowDataOffset - bytesPerPixel];

                        for (int leadingIndex = 0; leadingIndex < rowDataLength; leadingIndex++)
                        {
                            byte left = leftRef;
                            rawRef = (byte)(rawRef + left);
                            rawRef = ref Unsafe.Add(ref rawRef, 1);
                            leftRef = ref Unsafe.Add(ref leftRef, 1);
                        }

                        return;
                    }
                case 2:
                    {
                        // Up: raw[i] += previous[i]. Direct SIMD over entire row (no intra-row dependency) then scalar tail.
                        int vectorWidth = Vector<byte>.Count;
                        int vectorizable = Vector.IsHardwareAccelerated ? (rowDataLength - (rowDataLength % vectorWidth)) : 0;
                        for (int elementOffset = 0; elementOffset < vectorizable; elementOffset += vectorWidth)
                        {
                            var currentVector = Unsafe.ReadUnaligned<Vector<byte>>(currentRowPtr + (rowDataOffset + elementOffset));
                            var upVector = Unsafe.ReadUnaligned<Vector<byte>>(previousRowPtr + (rowDataOffset + elementOffset));
                            var decodedVector = currentVector + upVector;
                            decodedVector.CopyTo(currentRow, rowDataOffset + elementOffset);
                        }
                        int tailStart = vectorizable;
                        int tailCount = rowDataLength - tailStart;
                        if (tailCount > 0)
                        {
                            ref byte rawTailRef = ref currentRow[rowDataOffset + tailStart];
                            ref byte upTailRef = ref previousRow[rowDataOffset + tailStart];
                            for (int tailIndex = 0; tailIndex < tailCount; tailIndex++)
                            {
                                byte up = upTailRef;
                                rawTailRef = (byte)(rawTailRef + up);
                                rawTailRef = ref Unsafe.Add(ref rawTailRef, 1);
                                upTailRef = ref Unsafe.Add(ref upTailRef, 1);
                            }
                        }
                        return;
                    }
                case 3:
                    {
                        // Average: raw[i] += (left + up) >> 1.
                        ref byte rawRef = ref currentRow[rowDataOffset];
                        ref byte leftRef = ref currentRow[rowDataOffset - bytesPerPixel];
                        ref byte upRef = ref previousRow[rowDataOffset];

                        for (int leadingIndex = 0; leadingIndex < rowDataLength; leadingIndex++)
                        {
                            int left = leftRef;
                            int up = upRef;
                            rawRef = (byte)(rawRef + ((left + up) >> 1));
                            rawRef = ref Unsafe.Add(ref rawRef, 1);
                            leftRef = ref Unsafe.Add(ref leftRef, 1);
                            upRef = ref Unsafe.Add(ref upRef, 1);
                        }

                        return;
                    }
                case 4:
                    {
                        // Paeth: raw[i] += Paeth(left, up, upLeft). Kept scalar due to complexity and lower frequency.
                        ref byte rawRef = ref currentRow[rowDataOffset];
                        ref byte leftRef = ref currentRow[rowDataOffset - bytesPerPixel];
                        ref byte upRef = ref previousRow[rowDataOffset];
                        ref byte upLeftRef = ref previousRow[rowDataOffset - bytesPerPixel];
                        for (int i = 0; i < rowDataLength; i++)
                        {
                            int left = leftRef;
                            int up = upRef;
                            int upLeft = upLeftRef;
                            rawRef = (byte)(rawRef + Paeth(left, up, upLeft));
                            rawRef = ref Unsafe.Add(ref rawRef, 1);
                            leftRef = ref Unsafe.Add(ref leftRef, 1);
                            upRef = ref Unsafe.Add(ref upRef, 1);
                            upLeftRef = ref Unsafe.Add(ref upLeftRef, 1);
                        }
                        return;
                    }
                default:
                    {
                        // Unknown filter; treat as None.
                        return;
                    }
            }
        }

        /// <summary>
        /// Paeth predictor function per PNG specification. Returns one of a, b, c closest to p = a + b - c.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = p - a; if (pa < 0) { pa = -pa; }
            int pb = p - b; if (pb < 0) { pb = -pb; }
            int pc = p - c; if (pc < 0) { pc = -pc; }
            if (pa <= pb && pa <= pc) { return a; }
            if (pb <= pc) { return b; }
            return c;
        }
    }
}
