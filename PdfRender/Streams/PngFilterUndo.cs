using System;
using System.Runtime.CompilerServices;

namespace PdfRender.Streams
{
    /// <summary>
    /// PNG predictor filter undo implementation (types0..4).
    /// Layout expectation: caller supplies buffers with leading left margin of <paramref name="bytesPerPixel"/> zero bytes
    /// then a single filter byte, then pixel data (rowDataOffset points to first pixel data byte).
    /// </summary>
    internal static class PngFilterUndo
    {
        /// <summary>
        /// Undo a PNG predictor filter in-place on the current row buffer.
        /// </summary>
        /// <param name="filterType">PNG filter byte (0..4).</param>
        /// <param name="currentRow">Current encoded row buffer; modified in-place to decoded row values.</param>
        /// <param name="previousRow">Previously decoded row buffer (same layout) or zeros for first row.</param>
        /// <param name="bytesPerPixel">Bytes per pixel (components * bitsPerComponent rounded up to whole bytes).</param>
        /// <param name="rowDataOffset">Offset to the first pixel data byte (after margin + filter byte).</param>
        /// <param name="rowDataLength">Number of pixel data bytes in the row.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UndoPngFilter(byte filterType, byte[] currentRow, byte[] previousRow, int bytesPerPixel, int rowDataOffset, int rowDataLength)
        {
            if (currentRow == null)
            {
                throw new ArgumentNullException(nameof(currentRow));
            }
            if (previousRow == null)
            {
                throw new ArgumentNullException(nameof(previousRow));
            }
            if (bytesPerPixel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesPerPixel));
            }
            if (rowDataOffset < 0 || rowDataOffset > currentRow.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(rowDataOffset));
            }
            if (rowDataLength < 0 || rowDataOffset + rowDataLength > currentRow.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(rowDataLength));
            }

            switch (filterType)
            {
                case 0:
                {
                    // None: no transformation.
                    return;
                }
                case 1:
                {
                    // Sub: raw[i] += raw[i - bytesPerPixel]; first pixel's bytesPerPixel bytes have implicit left0.
                    for (int i = bytesPerPixel; i < rowDataLength; i++)
                    {
                        int idx = rowDataOffset + i;
                        currentRow[idx] = (byte)(currentRow[idx] + currentRow[idx - bytesPerPixel]);
                    }
                    return;
                }
                case 2:
                {
                    // Up: raw[i] += previous[i].
                    for (int i = 0; i < rowDataLength; i++)
                    {
                        int idx = rowDataOffset + i;
                        currentRow[idx] = (byte)(currentRow[idx] + previousRow[idx]);
                    }
                    return;
                }
                case 3:
                {
                    // Average: raw[i] += (left + up) >>1; left is zero for first pixel bytesPerPixel region.
                    for (int i = 0; i < rowDataLength; i++)
                    {
                        int idx = rowDataOffset + i;
                        int left = i >= bytesPerPixel ? currentRow[idx - bytesPerPixel] : 0;
                        int up = previousRow[idx];
                        currentRow[idx] = (byte)(currentRow[idx] + ((left + up) >> 1));
                    }
                    return;
                }
                case 4:
                {
                    // Paeth: raw[i] += Paeth(left, up, upLeft); left/upLeft zero for first pixel.
                    for (int i = 0; i < rowDataLength; i++)
                    {
                        int idx = rowDataOffset + i;
                        int left = i >= bytesPerPixel ? currentRow[idx - bytesPerPixel] : 0;
                        int up = previousRow[idx];
                        int upLeft = i >= bytesPerPixel ? previousRow[idx - bytesPerPixel] : 0;
                        currentRow[idx] = (byte)(currentRow[idx] + Paeth(left, up, upLeft));
                    }
                    return;
                }
                default:
                {
                    // Unknown filter type: treat as None.
                    return;
                }
            }
        }

        /// <summary>
        /// Paeth predictor per PNG specification: choose closest of a, b, c to p = a + b - c.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = p - a; if (pa < 0) { pa = -pa; }
            int pb = p - b; if (pb < 0) { pb = -pb; }
            int pc = p - c; if (pc < 0) { pc = -pc; }
            if (pa <= pb && pa <= pc)
            {
                return a;
            }
            if (pb <= pc)
            {
                return b;
            }
            return c;
        }
    }
}
