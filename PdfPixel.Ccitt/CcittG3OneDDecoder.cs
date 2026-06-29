using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Ccitt;

/// <summary>
/// CCITT Group 3 1-D (K = 0) bi-level decoder producing packed 1-bit output.
/// Output polarity: bit value depends on BlackIs1. If blackIs1==true then bit 1 = black else bit 0 = black.
/// Buffer layout: row-major, each row ((width + 7) / 8) bytes, MSB-first.
/// </summary>
public static class CcittG3OneDDecoder
{
    /// <summary>
    /// Decodes one CCITT G3 1-D encoded line into a run-length buffer.
    /// </summary>
    /// <param name="reader">Bit reader positioned at the start of the line.</param>
    /// <param name="width">Expected line width in pixels.</param>
    /// <param name="requireLeadingEol">When true, a leading EOL marker must be present and is consumed first.</param>
    /// <param name="byteAlign">When true, the reader is byte-aligned after consuming the leading EOL.</param>
    /// <param name="runs">Output buffer populated with alternating white/black run lengths (first run is white).</param>
    /// <param name="runsCount">On return, the number of runs written.</param>
    public static void DecodeOneDCollectRuns(
        ref CcittBitReader reader,
        int width,
        bool requireLeadingEol,
        bool byteAlign,
        in Span<int> runs,
        ref int runsCount)
    {
        runsCount = 0;

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
        var currentIsBlack = false;
        ref int runWrite = ref runs[0];

        while (xPosition < width)
        {
            RunDecodeResult result = CcittRunDecoder.DecodeRun(ref reader, currentIsBlack);
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

            if (runLength > width - xPosition)
            {
                throw new InvalidOperationException("CCITT G3 1D decode error: run overruns line (run=" + runLength + ", x=" + xPosition + ").");
            }

            runWrite = runLength;
            runWrite = ref Unsafe.Add(ref runWrite, 1);
            runsCount++;
            xPosition += runLength;
            currentIsBlack = !currentIsBlack;
        }

        if (xPosition != width)
        {
            throw new InvalidOperationException("CCITT G3 1D decode error: line incomplete (x=" + xPosition + ").");
        }
    }
}
