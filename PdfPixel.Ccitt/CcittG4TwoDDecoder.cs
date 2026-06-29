using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Ccitt;

/// <summary>
/// CCITT Group 4 2D decoder producing packed 1-bit output (row-major, MSB-first).
/// Polarity controlled by blackIs1 (true => bit 1 black, false => bit 0 black).
/// </summary>
public static class CcittG4TwoDDecoder
{
    /// <summary>
    /// Decodes one CCITT G4 2-D encoded line into a run-length buffer using the provided reference change positions.
    /// </summary>
    /// <param name="reader">Bit reader positioned at the start of the line.</param>
    /// <param name="width">Line width in pixels.</param>
    /// <param name="referenceChanges">Change positions from the previous reference row (color-transition x-coordinates).</param>
    /// <param name="runs">Output buffer populated with alternating white/black run lengths (first run is white).</param>
    /// <param name="runsCount">On return, the number of runs written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DecodeTwoDLine(ref CcittBitReader reader, int width, in ReadOnlySpan<int> referenceChanges, in Span<int> runs, ref int runsCount)
    {
        runsCount = 0;
        ref int runWrite = ref runs[0];

        int a0 = 0;
        int currentRunLength = 0;

        while (a0 < width)
        {
            if (!CcittModeReader.TryPeekAndConsumeMode(ref reader, out ModeCode mode))
            {
                int extPeek10 = reader.PeekBits(10);
                if (extPeek10 == 0b0000001000)
                {
                    throw new InvalidOperationException("CCITT G4 decode error: uncompressed extension mode encountered (not supported).");
                }

                if (reader.TryConsumeRtc())
                {
                    throw new InvalidOperationException("CCITT G4 decode error: premature RTC inside line.");
                }

                throw new InvalidOperationException("CCITT G4 decode error: cannot read mode a0=" + a0 + ".");
            }

            switch (mode.Type)
            {
                case ModeType.Pass:
                {
                    bool colorBefore = runsCount % 2 == 1;
                    GetB1B2(referenceChanges, a0, colorBefore, out int b1, out int b2);
                    if (b1 < a0 || b2 <= b1 || b2 > width)
                    {
                        throw new InvalidOperationException("CCITT G4 decode error: invalid pass pair a0=" + a0 + " b1=" + b1 + " b2=" + b2 + ".");
                    }

                    int extend = b2 - a0;
                    currentRunLength += extend;
                    a0 = b2;
                    break;
                }
                case ModeType.Vertical:
                {
                    bool colorBefore = runsCount % 2 == 1;
                    if (mode.VerticalDelta < -3 || mode.VerticalDelta > 3)
                    {
                        throw new InvalidOperationException("CCITT G4 decode error: vertical delta out of range (" + mode.VerticalDelta + ").");
                    }

                    int b1 = GetB1(referenceChanges, a0, colorBefore);
                    int a1 = b1 + mode.VerticalDelta;
                    if (a1 < a0 || a1 > width)
                    {
                        throw new InvalidOperationException("CCITT G4 decode error: vertical a1 invalid a0=" + a0 + " a1=" + a1 + " delta=" + mode.VerticalDelta + ".");
                    }

                    int run = a1 - a0;
                    currentRunLength += run;
                    runWrite = currentRunLength;
                    runWrite = ref Unsafe.Add(ref runWrite, 1);
                    runsCount++;
                    currentRunLength = 0;
                    a0 = a1;
                    break;
                }
                case ModeType.Horizontal:
                {
                    bool colorBefore = runsCount % 2 == 1;
                    RunDecodeResult firstRun = CcittRunDecoder.DecodeRun(ref reader, colorBefore);

                    if (!firstRun.HasTerminating)
                    {
                        throw new InvalidOperationException("CCITT G4 decode error: invalid first horizontal run a0=" + a0 + " len=" + firstRun.Length + ".");
                    }

                    if (a0 + firstRun.Length > width)
                    {
                        throw new InvalidOperationException("CCITT G4 decode error: first horizontal run overruns a0=" + a0 + " len=" + firstRun.Length + ".");
                    }

                    currentRunLength += firstRun.Length;
                    a0 += firstRun.Length;
                    runWrite = currentRunLength;
                    runWrite = ref Unsafe.Add(ref runWrite, 1);
                    runsCount++;
                    currentRunLength = 0;
                    bool colorAfterFirst = runsCount % 2 == 1;

                    RunDecodeResult secondRun = CcittRunDecoder.DecodeRun(ref reader, colorAfterFirst);

                    if (!secondRun.HasTerminating)
                    {
                        throw new InvalidOperationException("CCITT G4 decode error: invalid second horizontal run a0=" + a0 + " len=" + secondRun.Length + ".");
                    }

                    if (a0 + secondRun.Length > width)
                    {
                        throw new InvalidOperationException("CCITT G4 decode error: second horizontal run overruns a0=" + a0 + " len=" + secondRun.Length + ".");
                    }

                    currentRunLength += secondRun.Length;
                    a0 += secondRun.Length;
                    runWrite = currentRunLength;
                    runWrite = ref Unsafe.Add(ref runWrite, 1);
                    runsCount++;
                    currentRunLength = 0;
                    break;
                }
                default:
                {
                    throw new InvalidOperationException("CCITT G4 decode error: unsupported mode type " + mode.Type + ".");
                }
            }
        }

        if (currentRunLength > 0)
        {
            runWrite = currentRunLength;
            runsCount++;
        }
    }

    /// <summary>
    /// Finds the first reference change after a0 (or at 0 if a0 is 0) where the color after the change
    /// is not equal to a0Color. Returns the last change if no such change is found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetB1(in ReadOnlySpan<int> referenceChanges, int a0, bool a0Color)
    {
        ref readonly int start = ref referenceChanges[0];
        int length = referenceChanges.Length;

        for (int i = 0; i < length; i++)
        {
            int changePosition = start;

            if ((changePosition > a0 || (a0 == 0 && changePosition == 0)) && (i % 2) == 0 != a0Color)
            {
                return changePosition;
            }

            start = ref Unsafe.Add(ref Unsafe.AsRef(in start), 1);
        }

        return referenceChanges[referenceChanges.Length - 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetB1B2(in ReadOnlySpan<int> referenceChanges, int a0, bool a0Color, out int b1, out int b2)
    {
        ref readonly int start = ref referenceChanges[0];
        int length = referenceChanges.Length;

        for (int i = 0; i < length; i++)
        {
            int changePosition = start;
            if ((changePosition > a0 || (a0 == 0 && changePosition == 0)) && (i % 2) == 0 != a0Color)
            {
                b1 = changePosition;
                b2 = (i + 1 < referenceChanges.Length) ? referenceChanges[i + 1] : referenceChanges[referenceChanges.Length - 1];
                return;
            }

            start = ref Unsafe.Add(ref Unsafe.AsRef(in start), 1);
        }

        b1 = referenceChanges[referenceChanges.Length - 1];
        b2 = b1;
    }
}
