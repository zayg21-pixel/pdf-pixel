using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Ccitt
{
    /// <summary>
    /// CCITT Group 4 2D decoder producing packed 1-bit output (row-major, MSB-first).
    /// Polarity controlled by blackIs1 (true => bit 1 black, false => bit 0 black).
    /// </summary>
    internal static class CcittG4TwoDDecoder
    {
        internal static void DecodeTwoDLine(ref CcittBitReader reader, int width, List<int> referenceChanges, List<int> runs, List<int> nextReferenceChanges)
        {
            runs.Clear();
            nextReferenceChanges.Clear();

            int a0 = 0;
            int currentRunLength = 0;

            while (a0 < width)
            {
                bool currentColor = (runs.Count % 2) == 1;

                if (!CcittModeReader.TryPeekAndConsumeMode(ref reader, out var mode))
                {
                    int extPeek10 = reader.PeekBits(10);
                    if (extPeek10 == 0b0000001000)
                    {
                        throw new System.InvalidOperationException("CCITT G4 decode error: uncompressed extension mode encountered (not supported).");
                    }
                    if (reader.TryConsumeRtc())
                    {
                        throw new System.InvalidOperationException("CCITT G4 decode error: premature RTC inside line.");
                    }
                    throw new System.InvalidOperationException("CCITT G4 decode error: cannot read mode a0=" + a0 + ".");
                }

                switch (mode.Type)
                {
                    case ModeType.Pass:
                    {
                        bool colorBefore = (runs.Count % 2) == 1;
                        GetPassPair(referenceChanges, a0, colorBefore, out int b1, out int b2);
                        if (b1 < a0 || b2 <= b1 || b2 > width)
                        {
                            throw new System.InvalidOperationException("CCITT G4 decode error: invalid pass pair a0=" + a0 + " b1=" + b1 + " b2=" + b2 + ".");
                        }
                        int extend = b2 - a0;
                        currentRunLength += extend;
                        a0 = b2;
                        break;
                    }
                    case ModeType.Vertical:
                    {
                        bool colorBefore = (runs.Count % 2) == 1;
                        if (mode.VerticalDelta < -3 || mode.VerticalDelta > 3)
                        {
                            throw new System.InvalidOperationException("CCITT G4 decode error: vertical delta out of range (" + mode.VerticalDelta + ").");
                        }
                        int b1 = GetB1(referenceChanges, a0, colorBefore);
                        int a1 = b1 + mode.VerticalDelta;
                        if (a1 < a0 || a1 > width)
                        {
                            throw new System.InvalidOperationException("CCITT G4 decode error: vertical a1 invalid a0=" + a0 + " a1=" + a1 + " delta=" + mode.VerticalDelta + ".");
                        }
                        int run = a1 - a0;
                        currentRunLength += run;
                        FinalizeRun(runs, ref currentRunLength);
                        a0 = a1;
                        break;
                    }
                    case ModeType.Horizontal:
                    {
                        bool colorBefore = (runs.Count % 2) == 1;
                        var firstRun = CcittRunDecoder.DecodeRun(ref reader, colorBefore);
                        bool allowZeroFirst = (firstRun.Length == 0 && a0 == 0 && !colorBefore);
                        if ((firstRun.Length < 0 || (!allowZeroFirst && firstRun.Length == 0)) || !firstRun.HasTerminating || firstRun.IsEndOfLine)
                        {
                            throw new System.InvalidOperationException("CCITT G4 decode error: invalid first horizontal run a0=" + a0 + " len=" + firstRun.Length + ".");
                        }
                        if (a0 + firstRun.Length > width)
                        {
                            throw new System.InvalidOperationException("CCITT G4 decode error: first horizontal run overruns a0=" + a0 + " len=" + firstRun.Length + ".");
                        }
                        currentRunLength += firstRun.Length;
                        a0 += firstRun.Length;
                        FinalizeRun(runs, ref currentRunLength);
                        bool colorAfterFirst = (runs.Count % 2) == 1;
                        if (a0 >= width)
                        {
                            break;
                        }
                        var secondRun = CcittRunDecoder.DecodeRun(ref reader, colorAfterFirst);
                        if (secondRun.Length <= 0 || !secondRun.HasTerminating || secondRun.IsEndOfLine)
                        {
                            throw new System.InvalidOperationException("CCITT G4 decode error: invalid second horizontal run a0=" + a0 + " len=" + secondRun.Length + ".");
                        }
                        if (a0 + secondRun.Length > width)
                        {
                            throw new System.InvalidOperationException("CCITT G4 decode error: second horizontal run overruns a0=" + a0 + " len=" + secondRun.Length + ".");
                        }
                        currentRunLength += secondRun.Length;
                        a0 += secondRun.Length;
                        FinalizeRun(runs, ref currentRunLength);
                        break;
                    }
                    default:
                    {
                        throw new System.InvalidOperationException("CCITT G4 decode error: unsupported mode type " + mode.Type + ".");
                    }
                }
            }

            if (currentRunLength > 0)
            {
                FinalizeRun(runs, ref currentRunLength);
            }

            CcittRaster.BuildReferenceChangeList(runs, width, nextReferenceChanges);
        }

        private static void FinalizeRun(List<int> runs, ref int currentLength)
        {
            runs.Add(currentLength);
            currentLength = 0;
        }

        internal static int GetB1(List<int> referenceChanges, int a0, bool a0Color)
        {
            bool refColor = false;
            for (int i = 0; i < referenceChanges.Count; i++)
            {
                int change = referenceChanges[i];
                bool colorAfter = !refColor;
                if ((change > a0) || (a0 == 0 && change == 0))
                {
                    if (colorAfter != a0Color)
                    {
                        return change;
                    }
                }
                refColor = !refColor;
            }
            return referenceChanges[referenceChanges.Count - 1];
        }

        internal static void GetPassPair(List<int> referenceChanges, int a0, bool a0Color, out int b1, out int b2)
        {
            bool refColor = false;
            b1 = -1; b2 = -1;
            for (int i = 0; i < referenceChanges.Count; i++)
            {
                int change = referenceChanges[i];
                if (change > a0 || (a0 == 0 && change == 0))
                {
                    bool colorAfter = !refColor;
                    if (colorAfter != a0Color && b1 < 0)
                    {
                        b1 = change;
                        b2 = (i + 1 < referenceChanges.Count) ? referenceChanges[i + 1] : referenceChanges[referenceChanges.Count - 1];
                        return;
                    }
                }
                refColor = !refColor;
            }
            if (b1 < 0)
            {
                b1 = referenceChanges[referenceChanges.Count - 1];
                b2 = b1;
            }
        }
    }
}
