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
        subbands.Reset(new JpxRectangle(0, 0, 64, 48));

        uint state = 12345;
        Fill(subbands.LL, ref state);

        for (int level = 0; level < 3; level++)
        {
            Fill(subbands.GetSubband(level, JpxSubbandType.HL), ref state);
            Fill(subbands.GetSubband(level, JpxSubbandType.LH), ref state);
            Fill(subbands.GetSubband(level, JpxSubbandType.HH), ref state);
        }

        JpxInverseDwt97 transform = new(CreateQuantization(), BitDepth);
        int[] destination = new int[64 * 48];
        transform.Transform(subbands, destination, 1);

        Assert.Equal(32, subbands.GetResolutionWidth(1));
        Assert.Equal(24, subbands.GetResolutionHeight(1));
    }

    private static int[] Reconstruct(int levels, int originX, int originY, int width, int height)
    {
        JpxSubbandData subbands = new(levels);
        subbands.Reset(new JpxRectangle(originX, originY, width, height));

        // Deterministic pseudo-random sign-magnitude coefficients, as the Tier-1 decoder emits.
        uint state = (uint)((levels * PrimeLevels) + (originX * PrimeOriginX) + (originY * PrimeOriginY) + (width * PrimeWidth) + height);
        Fill(subbands.LL, ref state);

        for (int level = 0; level < levels; level++)
        {
            Fill(subbands.GetSubband(level, JpxSubbandType.HL), ref state);
            Fill(subbands.GetSubband(level, JpxSubbandType.LH), ref state);
            Fill(subbands.GetSubband(level, JpxSubbandType.HH), ref state);
        }

        JpxInverseDwt97 transform = new(CreateQuantization(), BitDepth);
        int[] destination = new int[subbands.GetResolutionWidth(0) * subbands.GetResolutionHeight(0)];
        transform.Transform(subbands, destination);

        return destination;
    }

    private static void Fill(in Span<int> coefficients, ref uint state)
    {
        for (int i = 0; i < coefficients.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;

            // Magnitude in the upper bits, as Tier-1 leaves it, plus an independent sign bit.
            coefficients[i] = (int)((state & 0x7FFFFFFF) | ((state & 1) << 31));
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
