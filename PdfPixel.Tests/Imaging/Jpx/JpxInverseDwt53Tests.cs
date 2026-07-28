using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Exercises the reversible 5-3 inverse DWT directly. Every JPX asset in the suite uses the
/// irreversible 9-7 kernel, so nothing else reaches this code path.
/// </summary>
public class JpxInverseDwt53Tests
{
    // Sqcd: no quantization (reversible) with two guard bits, per ITU-T T.800 Table A.28.
    private const byte ReversibleStyle = 2 << 5;

    private const int PrimeLevels = 73856093;
    private const int PrimeOriginX = 19349663;
    private const int PrimeOriginY = 83492791;
    private const int PrimeWidth = 15485863;

    /// <summary>
    /// Runs the transform over a spread of tile origins, sizes and decomposition depths, so
    /// both sample parities and the odd-size boundary cases are covered, and pins a digest of
    /// every reconstructed sample.
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

        Assert.Equal(0xC4B56F3364EC106BUL, digest);
    }

    /// <summary>
    /// A transform of a single-sample component is the degenerate case of the lifting steps
    /// and must pass the dequantized coefficient through unchanged.
    /// </summary>
    [Fact]
    public void Transform_SingleSample_PassesCoefficientThrough()
    {
        JpxQuantization quantization = CreateQuantization();
        JpxSubbandData subbands = new(1);
        subbands.Reset(new JpxRectangle(0, 0, 1, 1));
        subbands.LL[0] = 1 << 30;

        JpxInverseDwt53 transform = new(quantization);
        int[] destination = new int[1];
        transform.Transform(subbands, destination);

        int expected = JpxDequantizer.DequantizeReversible(1 << 30, JpxDequantizer.ComputeReversibleShift(quantization, 0));
        Assert.Equal(expected, destination[0]);
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

        JpxInverseDwt53 transform = new(CreateQuantization());
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
            Style = ReversibleStyle,
            StepSizes = new ushort[] { 8 << 11, 9 << 11, 9 << 11, 10 << 11, 9 << 11, 9 << 11, 10 << 11, 9 << 11, 9 << 11, 10 << 11 },
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
