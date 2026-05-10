using PdfPixel.Imaging.Jpx.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Inverse 5-3 reversible discrete wavelet transform per ITU-T T.800 Annex F.3.5.
/// Reconstructs component samples from subband coefficients using integer lifting.
/// Includes dequantization (right-shift from MSB-aligned Tier-1 output).
/// </summary>
internal sealed class JpxInverseDwt53 : IJpxInverseDwt
{
    private readonly JpxQuantization _quantization;

    // Reusable column buffer for cache-friendly column lifting
    private int[] _columnBuffer = Array.Empty<int>();

    // Reusable interleaved buffer to avoid per-level allocation
    private int[] _interleavedBuffer = Array.Empty<int>();

    /// <summary>
    /// Creates an inverse 5-3 DWT instance with quantization parameters for dequantization.
    /// </summary>
    /// <param name="quantization">Quantization parameters from QCD/QCC marker.</param>
    public JpxInverseDwt53(JpxQuantization quantization)
    {
        _quantization = quantization ?? throw new ArgumentNullException(nameof(quantization));
    }

    /// <inheritdoc/>
    public void Transform(JpxSubbandData subbands, int[] destination, int stopAtLevel = 0)
    {
        if (subbands == null)
        {
            throw new ArgumentNullException(nameof(subbands));
        }

        int fullWidth = subbands.FullWidth;
        int fullHeight = subbands.FullHeight;

        // Compute output dimensions based on how many levels we reconstruct
        int outputWidth = (stopAtLevel == 0) ? fullWidth : (fullWidth + (1 << stopAtLevel) - 1) >> stopAtLevel;
        int outputHeight = (stopAtLevel == 0) ? fullHeight : (fullHeight + (1 << stopAtLevel) - 1) >> stopAtLevel;

        int currentWidth = subbands.LLWidth;
        int currentHeight = subbands.LLHeight;

        // Dequantize LL subband (right-shift from MSB-aligned representation)
        int llShift = JpxDequantizer.ComputeReversibleShift(_quantization, 0);

        // Use destination as working buffer directly — no separate allocation needed
        for (int y = 0; y < currentHeight; y++)
        {
            for (int x = 0; x < currentWidth; x++)
            {
                destination[y * outputWidth + x] = JpxDequantizer.DequantizeReversible(subbands.LL[y * subbands.LLWidth + x], llShift);
            }
        }

        // Ensure reusable buffers are large enough
        int maxDimension = Math.Max(outputWidth, outputHeight);
        if (_columnBuffer.Length < maxDimension)
        {
            _columnBuffer = new int[maxDimension];
        }

        int maxPixels = outputWidth * outputHeight;
        if (_interleavedBuffer.Length < maxPixels)
        {
            _interleavedBuffer = new int[maxPixels];
        }

        // Reconstruct level by level from coarsest to finest
        for (int level = subbands.Levels - 1; level >= stopAtLevel; level--)
        {
            int nextWidth = (level == 0) ? fullWidth : (fullWidth + (1 << level) - 1) >> level;
            int nextHeight = (level == 0) ? fullHeight : (fullHeight + (1 << level) - 1) >> level;

            int hlWidth = subbands.GetWidth(level, JpxSubbandType.HL);
            int hlHeight = subbands.GetHeight(level, JpxSubbandType.HL);
            int lhWidth = subbands.GetWidth(level, JpxSubbandType.LH);
            int lhHeight = subbands.GetHeight(level, JpxSubbandType.LH);
            int hhWidth = subbands.GetWidth(level, JpxSubbandType.HH);
            int hhHeight = subbands.GetHeight(level, JpxSubbandType.HH);

            var hl = subbands.GetSubband(level, JpxSubbandType.HL);
            var lh = subbands.GetSubband(level, JpxSubbandType.LH);
            var hh = subbands.GetSubband(level, JpxSubbandType.HH);

            // QCD step size indices: LL=0, then per level (coarsest first) HL, LH, HH
            int qcdBase = 1 + (subbands.Levels - 1 - level) * 3;
            int hlShift = JpxDequantizer.ComputeReversibleShift(_quantization, qcdBase + 0);
            int lhShift = JpxDequantizer.ComputeReversibleShift(_quantization, qcdBase + 1);
            int hhShift = JpxDequantizer.ComputeReversibleShift(_quantization, qcdBase + 2);

            int[] interleaved = _interleavedBuffer;

            // LL → even rows, even columns (from destination, already dequantized)
            for (int y = 0; y < currentHeight && y * 2 < nextHeight; y++)
            {
                for (int x = 0; x < currentWidth && x * 2 < nextWidth; x++)
                {
                    interleaved[(y * 2) * nextWidth + (x * 2)] = destination[y * outputWidth + x];
                }
            }

            // HL → even rows, odd columns (dequantize)
            for (int y = 0; y < hlHeight && y * 2 < nextHeight; y++)
            {
                for (int x = 0; x < hlWidth && x * 2 + 1 < nextWidth; x++)
                {
                    interleaved[(y * 2) * nextWidth + (x * 2 + 1)] = JpxDequantizer.DequantizeReversible(hl[y * hlWidth + x], hlShift);
                }
            }

            // LH → odd rows, even columns (dequantize)
            for (int y = 0; y < lhHeight && y * 2 + 1 < nextHeight; y++)
            {
                for (int x = 0; x < lhWidth && x * 2 < nextWidth; x++)
                {
                    interleaved[(y * 2 + 1) * nextWidth + (x * 2)] = JpxDequantizer.DequantizeReversible(lh[y * lhWidth + x], lhShift);
                }
            }

            // HH → odd rows, odd columns (dequantize)
            for (int y = 0; y < hhHeight && y * 2 + 1 < nextHeight; y++)
            {
                for (int x = 0; x < hhWidth && x * 2 + 1 < nextWidth; x++)
                {
                    interleaved[(y * 2 + 1) * nextWidth + (x * 2 + 1)] = JpxDequantizer.DequantizeReversible(hh[y * hhWidth + x], hhShift);
                }
            }

            // Apply 1D inverse 5-3 filter on columns then rows
            for (int x = 0; x < nextWidth; x++)
            {
                InverseLiftColumn(interleaved, nextWidth, nextHeight, x, _columnBuffer);
            }

            for (int y = 0; y < nextHeight; y++)
            {
                InverseLiftRow(interleaved, nextWidth, y);
            }

            // Copy result for next level
            for (int y = 0; y < nextHeight; y++)
            {
                for (int x = 0; x < nextWidth; x++)
                {
                    destination[y * outputWidth + x] = interleaved[y * nextWidth + x];
                }
            }

            currentWidth = nextWidth;
            currentHeight = nextHeight;
        }
    }

    /// <summary>
    /// Applies inverse 5-3 lifting to a single column.
    /// Uses a contiguous buffer for cache-friendly sequential access.
    /// Inverse: x(2n) = s(n) - floor((d(n-1) + d(n) + 2) / 4)
    ///          x(2n+1) = d(n) + floor((x(2n) + x(2n+2)) / 2)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InverseLiftColumn(int[] data, int stride, int height, int x, int[] buffer)
    {
        if (height <= 1)
        {
            return;
        }

        int evenCount = (height + 1) >> 1;
        int oddCount = height >> 1;

        if (oddCount == 0)
        {
            return;
        }

        // Gather column into contiguous buffer
        for (int n = 0; n < height; n++)
        {
            buffer[n] = data[n * stride + x];
        }

        // Step 1: Undo update lifting on even samples (boundary-peeled)
        buffer[0] -= (buffer[1] + buffer[1] + 2) >> 2;
        for (int n = 1; n < evenCount - 1; n++)
        {
            buffer[n * 2] -= (buffer[n * 2 - 1] + buffer[n * 2 + 1] + 2) >> 2;
        }

        if (evenCount > 1)
        {
            int lastEven = (evenCount - 1) * 2;
            int dCurr = (evenCount - 1 < oddCount) ? buffer[lastEven + 1] : buffer[height - 2];
            buffer[lastEven] -= (buffer[lastEven - 1] + dCurr + 2) >> 2;
        }

        // Step 2: Undo predict lifting on odd samples (boundary-peeled)
        for (int n = 0; n < oddCount - 1; n++)
        {
            buffer[n * 2 + 1] += (buffer[n * 2] + buffer[(n + 1) * 2]) >> 1;
        }

        {
            int lastOdd = (oddCount - 1) * 2 + 1;
            int eNext = (oddCount < evenCount) ? buffer[oddCount * 2] : buffer[(oddCount - 1) * 2];
            buffer[lastOdd] += (buffer[(oddCount - 1) * 2] + eNext) >> 1;
        }

        // Scatter back to column
        for (int n = 0; n < height; n++)
        {
            data[n * stride + x] = buffer[n];
        }
    }

    /// <summary>
    /// Applies inverse 5-3 lifting to a single row.
    /// Boundary conditions are peeled out of main loops to avoid per-iteration branches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InverseLiftRow(int[] data, int width, int y)
    {
        if (width <= 1)
        {
            return;
        }

        int offset = y * width;
        int evenCount = (width + 1) >> 1;
        int oddCount = width >> 1;

        if (oddCount == 0)
        {
            return;
        }

        // Step 1: Undo update lifting on even samples (boundary-peeled)
        data[offset] -= (data[offset + 1] + data[offset + 1] + 2) >> 2;
        for (int n = 1; n < evenCount - 1; n++)
        {
            data[offset + n * 2] -= (data[offset + n * 2 - 1] + data[offset + n * 2 + 1] + 2) >> 2;
        }

        if (evenCount > 1)
        {
            int lastEvenIdx = offset + (evenCount - 1) * 2;
            int dCurr = (evenCount - 1 < oddCount) ? data[lastEvenIdx + 1] : data[offset + width - 2];
            data[lastEvenIdx] -= (data[lastEvenIdx - 1] + dCurr + 2) >> 2;
        }

        // Step 2: Undo predict lifting on odd samples (boundary-peeled)
        for (int n = 0; n < oddCount - 1; n++)
        {
            data[offset + n * 2 + 1] += (data[offset + n * 2] + data[offset + (n + 1) * 2]) >> 1;
        }

        {
            int lastOddIdx = offset + (oddCount - 1) * 2 + 1;
            int eNext = (oddCount < evenCount) ? data[offset + oddCount * 2] : data[offset + (oddCount - 1) * 2];
            data[lastOddIdx] += (data[offset + (oddCount - 1) * 2] + eNext) >> 1;
        }
    }
}
