using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Ccitt
{
    /// <summary>
    /// CCITT Group 3 1-D (K = 0) bi-level decoder producing packed 1-bit output.
    /// Output polarity: bit value depends on BlackIs1. If blackIs1==true then bit 1 = black else bit 0 = black.
    /// Buffer layout: row-major, each row ((width + 7) / 8) bytes, MSB-first.
    /// </summary>
    internal static class CcittG3OneDDecoder
    {
        public static void Decode(ReadOnlySpan<byte> data, Span<byte> buffer, int width, int height, bool blackIs1, bool endOfLine, bool byteAlign)
        {
            var reader = new CcittBitReader(data);
            var runs = new List<int>(256);

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                DecodeOneDCollectRuns(
                    ref reader,
                    width,
                    requireLeadingEol: endOfLine,
                    byteAlign: byteAlign,
                    runs: runs);

                CcittRaster.ValidateRunLengths(runs, width, rowIndex, "CCITT G3 1D");
                CcittRaster.RasterizeRuns(buffer, runs, rowIndex, width, blackIs1);
            }
        }

        internal static void DecodeOneDCollectRuns(
            ref CcittBitReader reader,
            int width,
            bool requireLeadingEol,
            bool byteAlign,
            List<int> runs)
        {
            if (runs == null)
            {
                throw new ArgumentNullException(nameof(runs));
            }

            runs.Clear();

            if (requireLeadingEol)
            {
                if (!reader.TryConsumeEol())
                {
                    throw new InvalidOperationException("CCITT G3 1D decode error: missing required leading EOL.");
                }
                if (byteAlign)
                {
                    reader.AlignAfterEndOfLine(true);
                }
            }

            int xPosition = 0;
            bool currentIsBlack = false;
            bool leadingZeroAllowed = true;

            while (xPosition < width)
            {
                var result = CcittRunDecoder.DecodeRun(ref reader, currentIsBlack);
                if (result.Length < 0)
                {
                    throw new InvalidOperationException("CCITT G3 1D decode error: invalid code at x=" + xPosition + ".");
                }
                if (result.IsEndOfLine)
                {
                    if (xPosition != width)
                    {
                        throw new InvalidOperationException("CCITT G3 1D decode error: premature EOL at x=" + xPosition + ".");
                    }
                    break;
                }
                if (!result.HasTerminating)
                {
                    throw new InvalidOperationException("CCITT G3 1D decode error: missing terminating code at x=" + xPosition + ".");
                }

                int runLength = result.Length;
                if (runLength == 0)
                {
                    if (xPosition == 0 && !currentIsBlack && leadingZeroAllowed)
                    {
                        runs.Add(0);
                        currentIsBlack = true;
                        leadingZeroAllowed = false;
                        continue;
                    }
                    throw new InvalidOperationException("CCITT G3 1D decode error: zero-length run encountered mid line at x=" + xPosition + ".");
                }
                if (runLength > width - xPosition)
                {
                    throw new InvalidOperationException("CCITT G3 1D decode error: run overruns line (run=" + runLength + ", x=" + xPosition + ").");
                }

                runs.Add(runLength);
                xPosition += runLength;
                currentIsBlack = !currentIsBlack;
            }

            if (xPosition != width)
            {
                throw new InvalidOperationException("CCITT G3 1D decode error: line incomplete (x=" + xPosition + ").");
            }
        }
    }
}
