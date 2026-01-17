using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfRender.Imaging.Ccitt;

/// <summary>
/// Utility methods for validating runs and writing CCITT decoded runs into a packed 1-bit buffer.
/// Bit semantics: if BlackIs1 is true then bit 1 encodes black pixels (white = 0). Otherwise bit 0 encodes black (white = 1).
/// The first run in <paramref name="runs"/> is white (may be zero length) per CCITT specification.
/// </summary>
internal static class CcittRaster
{
    /// <summary>
    /// Rasterize run-length data into the packed bit buffer. First run is white (may be zero) and colors alternate.
    /// Background was pre-initialized to white; only the black runs are written by flipping bits when necessary.
    /// </summary>
    /// <param name="buffer">Packed 1-bit buffer.</param>
    /// <param name="runs">Run lengths (first white) alternating white/black.</param>
    /// <param name="rowIndex">Row index being written.</param>
    /// <param name="width">Row width in pixels.</param>
    /// <param name="blackIs1">Bit polarity (1=black when true).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RasterizeRuns(Span<byte> buffer, List<int> runs, int rowIndex, int width, bool blackIs1)
    {
        int rowBytes = (width + 7) / 8;
        int rowBase = rowIndex * rowBytes;
        int x = 0;
        bool isBlack = false; // first run white
        int blackBit = blackIs1 ? 1 : 0;

        for (int r = 0; r < runs.Count; r++)
        {
            int runLength = runs[r];
            if (runLength > 0 && isBlack)
            {
                WriteBlackRun(buffer, rowBase, x, runLength, blackBit);
            }
            x += runLength;
            isBlack = !isBlack;
        }
    }

    /// <summary>
    /// Build reference change list from run lengths for subsequent 2D line processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BuildReferenceChangeList(List<int> runs, int width, int[] buffer)
    {
        int position = 0;
        if (runs.Count > 0 && runs[0] == 0)
        {
            buffer[position] = 0;
            position++;
        }

        int accumulator = 0;
        for (int i = 0; i < runs.Count; i++)
        {
            accumulator += runs[i];
            if (accumulator > 0 && accumulator < width)
            {
                buffer[position] = accumulator;
                position++;
            }
        }
        if (position == 0 || buffer[position - 1] != width)
        {
            buffer[position] = width;
            position++;
        }

        return position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBlackRun(Span<byte> buffer, int rowBase, int startX, int length, int blackBit)
    {
        if (length <= 0)
        {
            return;
        }
        int endX = startX + length;
        int startByte = startX >> 3;
        int endByte = endX - 1 >> 3;
        int rowOffsetStart = rowBase + startByte;
        int bitStart = startX & 7;
        int bitEnd = endX - 1 & 7;
        // When whiteBit ==1 and blackBit==0 we clear bits to 0 for black; when whiteBit==0 and blackBit==1 we set bits.
        bool setBits = blackBit == 1;

        if (startByte == endByte)
        {
            int mask = 0xFF >> bitStart & 0xFF << 7 - bitEnd;
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
        int lastMask = 0xFF << 7 - bitEnd;
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
