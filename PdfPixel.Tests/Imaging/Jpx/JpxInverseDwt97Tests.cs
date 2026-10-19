using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using System;
using Xunit;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Exercises the irreversible 9-7 inverse DWT over tile geometries the sample images do not
/// produce, so both sample parities and the degenerate boundary cases stay covered.
/// </summary>
public class JpxInverseDwt97Tests
{
    // Sqcd: scalar expounded quantization with two guard bits, per ITU-T T.800 Table A.28.
    private const byte ExpoundedStyle = (2 << 5) | 2;

    private const int BitDepth = 8;

    private const int PrimeLevels = 73856093;
    private const int PrimeOriginX = 19349663;
    private const int PrimeOriginY = 83492791;
    private const int PrimeWidth = 15485863;

    /// <summary>
    /// Runs the transform over a spread of tile origins, sizes and decomposition depths and
    /// pins a digest of every reconstructed sample.
    /// </summary>
    [Fact]
    public void Transform_ReconstructsExpectedSamples()
    {
        ulong digest = 1469598103934665603UL;

        foreach (int levels in new[] { 1, 2, 3 })
        {
            foreach (int originX in new[] { 0, 1 })
            {
                foreach (int originY in new[] { 0, 1 })
                {
                    foreach (int width in new[] { 1, 2, 5, 16, 37 })
                    {
                        foreach (int height in new[] { 1, 3, 8, 29 })
                        {
                            digest = Accumulate(digest, Reconstruct(levels, originX, originY, width, height));
                        }
                    }
                }
            }
        }

        Assert.Equal(0xEBFDC3950C725DFFUL, digest);
    }

    /// <summary>
    /// Reduced-resolution decoding stops before the finest levels, so the transform must
    /// produce the coarser resolution without touching the levels it skips.
    /// </summary>
    [Fact]
    public void Transform_StoppingEarly_ProducesCoarserResolution()
    {
        JpxSubbandData subbands = new(3);
        subbands.Reset(new JpxRectangle(0, 0, 64, 48), new int[64 * 48], 1);

        uint state = 12345;
        Fill(subbands, 0, subbands.LLWidth, subbands.LLHeight, ref state);

        for (int level = 1; level < 3; level++)
        {
            Fill(subbands, level, JpxSubbandType.HL, ref state);
            Fill(subbands, level, JpxSubbandType.LH, ref state);
            Fill(subbands, level, JpxSubbandType.HH, ref state);
        }

        JpxInverseDwt97 transform = new(CreateQuantization(), BitDepth);
        transform.Transform(subbands, new JpxDwtScratch(), 1);

        Assert.Equal(32, subbands.GetResolutionWidth(1));
        Assert.Equal(24, subbands.GetResolutionHeight(1));
        Assert.Equal(32, subbands.Stride);
        Assert.Equal(32 * 24, subbands.Samples.Length);
    }

    private static int[] Reconstruct(int levels, int originX, int originY, int width, int height)
    {
        JpxSubbandData subbands = new(levels);
        subbands.Reset(new JpxRectangle(originX, originY, width, height), new int[width * height]);

        // Deterministic pseudo-random sign-magnitude coefficients, as the Tier-1 decoder emits.
        uint state = (uint)((levels * PrimeLevels) + (originX * PrimeOriginX) + (originY * PrimeOriginY) + (width * PrimeWidth) + height);
        Fill(subbands, 0, subbands.LLWidth, subbands.LLHeight, ref state);

        for (int level = 0; level < levels; level++)
        {
            Fill(subbands, level, JpxSubbandType.HL, ref state);
            Fill(subbands, level, JpxSubbandType.LH, ref state);
            Fill(subbands, level, JpxSubbandType.HH, ref state);
        }

        JpxInverseDwt97 transform = new(CreateQuantization(), BitDepth);
        transform.Transform(subbands, new JpxDwtScratch());

        return subbands.Samples.ToArray();
    }

    private static void Fill(JpxSubbandData subbands, int level, JpxSubbandType type, ref uint state)
        => Fill(subbands, subbands.GetOffset(level, type), subbands.GetWidth(level, type), subbands.GetHeight(level, type), ref state);

    private static void Fill(JpxSubbandData subbands, int offset, int width, int height, ref uint state)
    {
        Span<int> samples = subbands.Samples;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;

                // Magnitude in the upper bits, as Tier-1 leaves it, plus an independent sign bit.
                samples[offset + (y * subbands.Stride) + x] = (int)((state & 0x7FFFFFFF) | ((state & 1) << 31));
            }
        }
    }

    private static JpxQuantization CreateQuantization()
    {
        JpxQuantization quantization = new()
        {
            Style = ExpoundedStyle,
            StepSizes = new ushort[]
            {
                (ushort)((8 << 11) | 1024),
                (ushort)((9 << 11) | 512),
                (ushort)((9 << 11) | 512),
                (ushort)((10 << 11) | 256),
                (ushort)((9 << 11) | 1536),
                (ushort)((9 << 11) | 1536),
                (ushort)((10 << 11) | 768),
                (ushort)((9 << 11) | 128),
                (ushort)((9 << 11) | 128),
                (ushort)((10 << 11) | 64),
            },
        };

        return quantization;
    }

    private static ulong Accumulate(ulong digest, int[] samples)
    {
        foreach (int sample in samples)
        {
            uint value = (uint)sample;

            for (int byteIndex = 0; byteIndex < 4; byteIndex++)
            {
                digest = (digest ^ ((value >> (byteIndex * 8)) & 0xFF)) * 1099511628211UL;
            }
        }

        return digest;
    }
}
