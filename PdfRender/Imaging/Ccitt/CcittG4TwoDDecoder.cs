using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfRender.Imaging.Ccitt;

/// <summary>
/// CCITT Group 4 2D decoder producing packed 1-bit output (row-major, MSB-first).
/// Polarity controlled by blackIs1 (true => bit 1 black, false => bit 0 black).
/// </summary>
internal static class CcittG4TwoDDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeTwoDLine(ref CcittBitReader reader, int width, Span<int> referenceChanges, List<int> runs)
    {
        runs.Clear();

        int a0 = 0;
        int currentRunLength = 0;

        while (a0 < width)
        {
            if (!CcittModeReader.TryPeekAndConsumeMode(ref reader, out var mode))
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
                    bool colorBefore = runs.Count % 2 == 1;
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
                    bool colorBefore = runs.Count % 2 == 1;
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
                    FinalizeRun(runs, ref currentRunLength);
                    a0 = a1;
                    break;
                }
                case ModeType.Horizontal:
                {
                    bool colorBefore = runs.Count % 2 == 1;
                    var firstRun = CcittRunDecoder.DecodeRun(ref reader, colorBefore);
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
                    FinalizeRun(runs, ref currentRunLength);
                    bool colorAfterFirst = runs.Count % 2 == 1;

                    var secondRun = CcittRunDecoder.DecodeRun(ref reader, colorAfterFirst);
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
                    FinalizeRun(runs, ref currentRunLength);
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
            FinalizeRun(runs, ref currentRunLength);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FinalizeRun(List<int> runs, ref int currentLength)
    {
        runs.Add(currentLength);
        currentLength = 0;
    }

    /// <summary>
    /// Finds the first reference change after a0 (or at 0 if a0 is 0) where the color after the change
    /// is not equal to a0Color. Returns the last change if no such change is found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetB1(Span<int> referenceChanges, int a0, bool a0Color)
    {
        ref int start = ref referenceChanges[0];
        int length = referenceChanges.Length;

        for (int i = 0; i < length; i++)
        {
            int changePosition = start;

            if ((changePosition > a0 || (a0 == 0 && changePosition == 0)) && (i % 2) == 0 != a0Color)
            {
                return changePosition;
            }

            start = ref Unsafe.Add(ref start, 1);
        }
        // If not found, return the last change
        return referenceChanges[referenceChanges.Length - 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetB1B2(Span<int> referenceChanges, int a0, bool a0Color, out int b1, out int b2)
    {
        ref int start = ref referenceChanges[0];
        int length = referenceChanges.Length;

        for (int i = 0; i < length; i++)
        {
            int changePosition = start;
            if ((changePosition > a0 || (a0 == 0 && changePosition == 0)) && (i % 2) == 0 != a0Color)
            {
                b1 = changePosition;
                b2 = i + 1 < referenceChanges.Length ? referenceChanges[i + 1] : referenceChanges[referenceChanges.Length - 1];
                return;
            }

            start = ref Unsafe.Add(ref start, 1);
        }
        // Fallback: assign last change for both b1 and b2
        b1 = referenceChanges[referenceChanges.Length - 1];
        b2 = b1;
    }
}
