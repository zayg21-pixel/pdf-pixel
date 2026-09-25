using System;
using System.Collections.Generic;
using PdfPixel.Ccitt;
using Xunit;

namespace PdfPixel.Tests.Imaging.Ccitt;

/// <summary>
/// Covers the reference line search of the Group 4 two-dimensional decoder. ITU-T T.4 4.2.1.3.1, which T.6 uses,
/// defines b1 as the first changing element on the reference line to the right of a0 and of opposite color to a0, and
/// b2 as the next changing element after b1. The decoder resumes each search where the line's previous search stopped,
/// so these tests hold the pixels it decodes, and every b1 and b2 it finds, to a search from the first change.
/// </summary>
public class CcittG4TwoDDecoderTests
{
    private const int DitherWidth = 32;
    private const int DitherHeight = 16;

    // A 4 x 4 Bayer matrix orders 16 thresholds, so the gradient has 17 levels, from 0 (white) to 16 (black).
    private const int GradientLevels = 17;

    private const int RandomSeed = 1728;
    private const int ReferenceLineCount = 2000;
    private const int MaximumLineWidth = 200;
    private const int SearchesPerLine = 64;
    private const int LongestStep = 4;
    private const int JumpOneIn = 12;

    private static readonly int[][] BayerThresholds =
    [
        [0, 8, 2, 10],
        [12, 4, 14, 6],
        [3, 11, 1, 9],
        [15, 7, 13, 5]
    ];

    // The 4 x 4 ordered dither of a diagonal gradient that CreateDitherRow describes, 32 x 16 pixels, encoded as
    // Group 4 by libtiff 4.7.1 (through Pillow 12.2.0): K < 0, no EOLs, not byte-aligned, ending in EOFB.
    private static readonly byte[] DitherCodestream =
    [
        0x36, 0x8C, 0x22, 0x3A, 0x23, 0xA2, 0x3A, 0x23, 0xA2, 0x3A, 0x23, 0xA2, 0x3A, 0x23, 0xC4, 0x76,
        0x22, 0x2D, 0x34, 0x92, 0x48, 0x10, 0x24, 0x8A, 0x1C, 0x25, 0x3C, 0x81, 0x02, 0x48, 0x10, 0x24,
        0x92, 0x49, 0x24, 0x93, 0x51, 0x11, 0x69, 0xA4, 0x92, 0x40, 0x81, 0x24, 0x12, 0x93, 0x54, 0x61,
        0x11, 0xD1, 0x1D, 0x11, 0xD0, 0x20, 0x49, 0x02, 0x04, 0x92, 0x68, 0x44, 0x62, 0x22, 0xD3, 0x49,
        0x24, 0x81, 0x02, 0x48, 0xA1, 0xCA, 0x1C, 0xA1, 0xC2, 0x52, 0xE8, 0x10, 0x24, 0x81, 0x02, 0x49,
        0x24, 0x92, 0x49, 0x34, 0x23, 0x11, 0x16, 0x9A, 0x49, 0x24, 0x08, 0x12, 0x45, 0x0E, 0x50, 0xE1,
        0x29, 0x35, 0x44, 0x74, 0x47, 0x44, 0x74, 0x08, 0x12, 0x40, 0x81, 0x24, 0x9A, 0x11, 0x11, 0x18,
        0x8B, 0x4D, 0x24, 0x92, 0x04, 0x09, 0x22, 0x87, 0x28, 0x72, 0x87, 0x28, 0x70, 0x94, 0x10, 0x24,
        0x81, 0x02, 0x49, 0x24, 0x92, 0x49, 0x34, 0x22, 0x31, 0x16, 0x9A, 0x49, 0x24, 0x08, 0x12, 0x45,
        0x0E, 0x50, 0xE5, 0x0E, 0x50, 0xF2, 0x6A, 0x88, 0xE8, 0x10, 0x24, 0x81, 0x02, 0x49, 0x34, 0x22,
        0x22, 0x23, 0x69, 0xA4, 0x92, 0x40, 0x81, 0x24, 0x50, 0xE5, 0x0E, 0x50, 0xE5, 0x0E, 0x61, 0xE9,
        0x02, 0x04, 0x92, 0x49, 0x24, 0x92, 0x68, 0x44, 0x62, 0xD3, 0x49, 0x24, 0x81, 0x02, 0x48, 0xA1,
        0xCA, 0x1C, 0xA1, 0xCA, 0x1C, 0xC3, 0xC0, 0x04, 0x00, 0x40
    ];

    /// <summary>
    /// A dither puts changing elements all along its lines, so every line runs the search many times. The decoded
    /// rows have to be the image's pixels exactly; its lines use pass, horizontal and vertical codes, and some begin
    /// black.
    /// </summary>
    [Fact]
    public void Decode_OrderedDither_ReproducesPixels()
    {
        CcittRowDecoder decoder = new(
            DitherCodestream,
            DitherWidth,
            DitherHeight,
            blackIs1: false,
            k: -1,
            endOfLine: false,
            byteAlign: false,
            endOfBlock: true);
        byte[] row = new byte[decoder.RowStride];

        for (int y = 0; y < DitherHeight; y++)
        {
            Assert.True(decoder.DecodeNextRow(row));
            Assert.Equal(CreateDitherRow(y), row);
        }

        Assert.False(decoder.DecodeNextRow(row));
    }

    /// <summary>
    /// Every b1 and b2 the resumed search finds has to be the one a search from the first change finds. The reference
    /// lines are built the way the decoder builds them: sorted, closed by the line's width, opening with a change at 0
    /// when the line begins black, and repeating a change where two changes coincide. A few edge cases come first. a0
    /// moves right along each line and now and then jumps, sometimes to the left, where the search has to start again.
    /// </summary>
    [Fact]
    public void ReferenceSearch_ResumedAlongLine_MatchesSearchFromFirstChange()
    {
        Random random = new(RandomSeed);

        foreach (int[] referenceChanges in CreateReferenceLines(random))
        {
            int width = referenceChanges[^1];
            int searchStart = 0;
            int searchA0 = 0;
            int a0 = 0;

            for (int search = 0; search < SearchesPerLine; search++)
            {
                bool a0Color = random.Next(2) == 1;
                (int expectedB1, int expectedB2) = SearchFromFirstChange(referenceChanges, a0, a0Color);

                int b1 = CcittG4TwoDDecoder.GetB1(referenceChanges, a0, a0Color, ref searchStart, ref searchA0);
                CcittG4TwoDDecoder.GetB1B2(referenceChanges, a0, a0Color, ref searchStart, ref searchA0, out int pairB1, out int pairB2);

                if (b1 != expectedB1 || pairB1 != expectedB1 || pairB2 != expectedB2)
                {
                    Assert.Fail(
                        $"Reference line [{string.Join(", ", referenceChanges)}], a0 {a0}, a0Color {a0Color}: "
                        + $"GetB1 gave {b1} and GetB1B2 ({pairB1}, {pairB2}), where the search from the first change gives ({expectedB1}, {expectedB2}).");
                }

                a0 = NextA0(random, a0, width);
            }
        }
    }

    private static byte[] CreateDitherRow(int y)
    {
        // A pixel is black where its Bayer threshold lies below the gradient's level. With BlackIs1 false the decoder
        // writes black as a 0 bit and white as a 1 bit, most significant bit first.
        byte[] row = new byte[(DitherWidth + 7) / 8];
        int gradientSpan = DitherWidth + DitherHeight - 2;

        for (int x = 0; x < DitherWidth; x++)
        {
            int level = (x + y) * GradientLevels / gradientSpan;
            bool isBlack = BayerThresholds[y % 4][x % 4] < level;
            if (!isBlack)
            {
                row[x / 8] |= (byte)(0x80 >> (x % 8));
            }
        }

        return row;
    }

    private static IEnumerable<int[]> CreateReferenceLines(Random random)
    {
        // An all-white line, an all-black line, the narrowest lines, a change at every pixel, and repeated changes.
        yield return [8];
        yield return [0, 8];
        yield return [1];
        yield return [0, 1];
        yield return [1, 2, 3, 4, 5, 6, 7, 8];
        yield return [0, 1, 2, 3, 4, 5, 6, 7, 8];
        yield return [3, 3, 5, 5, 5, 8];

        for (int line = 0; line < ReferenceLineCount; line++)
        {
            int width = random.Next(1, MaximumLineWidth + 1);
            int[] interiorChanges = new int[random.Next(0, width)];
            for (int i = 0; i < interiorChanges.Length; i++)
            {
                interiorChanges[i] = random.Next(1, width);
            }

            Array.Sort(interiorChanges);

            List<int> referenceChanges = [];
            if (random.Next(4) == 0)
            {
                referenceChanges.Add(0);
            }

            referenceChanges.AddRange(interiorChanges);
            referenceChanges.Add(width);

            yield return referenceChanges.ToArray();
        }
    }

    private static int NextA0(Random random, int a0, int width)
    {
        if (random.Next(JumpOneIn) == 0)
        {
            return random.Next(0, width + 1);
        }

        return Math.Min(width, a0 + random.Next(0, LongestStep + 1));
    }

    private static (int B1, int B2) SearchFromFirstChange(int[] referenceChanges, int a0, bool a0Color)
    {
        // b1 is the first change right of a0 (or the change at 0 while a0 is 0) whose color differs from a0Color, and b2
        // the change after it; both fall back to the last change. Even entries are changes to black.
        int lastChange = referenceChanges[^1];

        for (int i = 0; i < referenceChanges.Length; i++)
        {
            int changePosition = referenceChanges[i];
            bool isRightOfA0 = changePosition > a0 || (a0 == 0 && changePosition == 0);
            bool changesToBlack = i % 2 == 0;
            if (isRightOfA0 && changesToBlack != a0Color)
            {
                int nextChange = i + 1 < referenceChanges.Length ? referenceChanges[i + 1] : lastChange;
                return (changePosition, nextChange);
            }
        }

        return (lastChange, lastChange);
    }
}
