using PdfPixel.Jpx.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Inverse 9-7 irreversible discrete wavelet transform per ITU-T T.800 Annex F.3.6
/// with integrated inverse quantization. All arithmetic uses 13-bit fixed-point integers
/// to avoid float conversions and enable direct writing into the destination buffer.
/// </summary>
internal sealed class JpxInverseDwt97 : IJpxInverseDwt
{
    // CDF 9/7 lifting coefficients in 13-bit fixed-point (multiplied by 8192)
    private const int FracBits = 13;
    private const int Half = 1 << (FracBits - 1); // 4096, for rounding

    private const int AlphaFix = -12994;  // round(-1.586134342 * 8192)
    private const int BetaFix = -434;     // round(-0.052980118 * 8192)
    private const int GammaFix = 7233;    // round(0.882911075 * 8192)
    private const int DeltaFix = 3633;    // round(0.443506852 * 8192)
    private const int KFix = 10078;       // round(1.230174104914 * 8192)
    private const int InvKFix = 6659;     // round((1/1.230174104914) * 8192)

    private readonly JpxQuantization _quantization;
    private readonly int _bitDepth;

    // Reusable column buffer to avoid per-column allocation and improve cache locality
    private int[] _columnBuffer = Array.Empty<int>();

    // Reusable interleaved buffer to avoid per-level allocation
    private int[] _interleavedBuffer = Array.Empty<int>();

    /// <summary>
    /// Creates an inverse 9-7 DWT instance with the quantization parameters needed
    /// to dequantize coefficients during interleaving.
    /// </summary>
    /// <param name="quantization">Quantization parameters from QCD/QCC marker.</param>
    /// <param name="bitDepth">Component bit depth for computing nominal dynamic range.</param>
    public JpxInverseDwt97(JpxQuantization quantization, int bitDepth)
    {
        _quantization = quantization;
        _bitDepth = bitDepth;
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
        int levels = subbands.Levels;

        // Compute output dimensions based on how many levels we reconstruct
        int outputWidth = (stopAtLevel == 0) ? fullWidth : (fullWidth + (1 << stopAtLevel) - 1) >> stopAtLevel;
        int outputHeight = (stopAtLevel == 0) ? fullHeight : (fullHeight + (1 << stopAtLevel) - 1) >> stopAtLevel;

        int currentWidth = subbands.LLWidth;
        int currentHeight = subbands.LLHeight;

        // Dequantize LL into destination (used as working buffer)
        JpxDequantizer.IrreversibleStepParams llParams = JpxDequantizer.ComputeIrreversibleParams(_quantization, 0, 0, _bitDepth, FracBits);
        for (int y = 0; y < currentHeight; y++)
        {
            for (int x = 0; x < currentWidth; x++)
            {
                destination[(y * outputWidth) + x] = JpxDequantizer.DequantizeIrreversible(subbands.LL[(y * subbands.LLWidth) + x], llParams);
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
        for (int level = levels - 1; level >= stopAtLevel; level--)
        {
            int nextWidth = (level == 0) ? fullWidth : (fullWidth + (1 << level) - 1) >> level;
            int nextHeight = (level == 0) ? fullHeight : (fullHeight + (1 << level) - 1) >> level;

            int[] hl = subbands.GetSubband(level, JpxSubbandType.HL);
            int[] lh = subbands.GetSubband(level, JpxSubbandType.LH);
            int[] hh = subbands.GetSubband(level, JpxSubbandType.HH);
            int hlWidth = subbands.GetWidth(level, JpxSubbandType.HL);
            int hlHeight = subbands.GetHeight(level, JpxSubbandType.HL);
            int lhWidth = subbands.GetWidth(level, JpxSubbandType.LH);
            int lhHeight = subbands.GetHeight(level, JpxSubbandType.LH);
            int hhWidth = subbands.GetWidth(level, JpxSubbandType.HH);
            int hhHeight = subbands.GetHeight(level, JpxSubbandType.HH);

            // QCD step size indices: LL=0, then coarsest to finest detail
            // level (levels-1) → indices 1,2,3; level (levels-2) → 4,5,6; etc.
            int qcdBase = 1 + ((levels - 1 - level) * 3);
            JpxDequantizer.IrreversibleStepParams hlParams = JpxDequantizer.ComputeIrreversibleParams(_quantization, qcdBase + 0, 1, _bitDepth, FracBits);
            JpxDequantizer.IrreversibleStepParams lhParams = JpxDequantizer.ComputeIrreversibleParams(_quantization, qcdBase + 1, 1, _bitDepth, FracBits);
            JpxDequantizer.IrreversibleStepParams hhParams = JpxDequantizer.ComputeIrreversibleParams(_quantization, qcdBase + 2, 2, _bitDepth, FracBits);

            int[] interleaved = _interleavedBuffer;

            // LL (from destination) → even rows, even columns
            for (int y = 0; y < currentHeight && y * 2 < nextHeight; y++)
            {
                for (int x = 0; x < currentWidth && x * 2 < nextWidth; x++)
                {
                    interleaved[(y * 2 * nextWidth) + (x * 2)] = destination[(y * outputWidth) + x];
                }
            }

            // HL → even rows, odd columns (dequantize during copy)
            for (int y = 0; y < hlHeight && y * 2 < nextHeight; y++)
            {
                for (int x = 0; x < hlWidth && (x * 2) + 1 < nextWidth; x++)
                {
                    interleaved[(y * 2 * nextWidth) + ((x * 2) + 1)] = JpxDequantizer.DequantizeIrreversible(hl[(y * hlWidth) + x], hlParams);
                }
            }

            // LH → odd rows, even columns
            for (int y = 0; y < lhHeight && (y * 2) + 1 < nextHeight; y++)
            {
                for (int x = 0; x < lhWidth && x * 2 < nextWidth; x++)
                {
                    interleaved[(((y * 2) + 1) * nextWidth) + (x * 2)] = JpxDequantizer.DequantizeIrreversible(lh[(y * lhWidth) + x], lhParams);
                }
            }

            // HH → odd rows, odd columns
            for (int y = 0; y < hhHeight && (y * 2) + 1 < nextHeight; y++)
            {
                for (int x = 0; x < hhWidth && (x * 2) + 1 < nextWidth; x++)
                {
                    interleaved[(((y * 2) + 1) * nextWidth) + ((x * 2) + 1)] = JpxDequantizer.DequantizeIrreversible(hh[(y * hhWidth) + x], hhParams);
                }
            }

            // Apply 1D inverse 9-7 filter on columns (using temp buffer for locality) then rows
            for (int x = 0; x < nextWidth; x++)
            {
                InverseLiftColumn(interleaved, nextWidth, nextHeight, x, _columnBuffer);
            }

            for (int y = 0; y < nextHeight; y++)
            {
                InverseLiftRow(interleaved, nextWidth, y);
            }

            // Copy to destination for next level
            for (int y = 0; y < nextHeight; y++)
            {
                for (int x = 0; x < nextWidth; x++)
                {
                    destination[(y * outputWidth) + x] = interleaved[(y * nextWidth) + x];
                }
            }

            currentWidth = nextWidth;
            currentHeight = nextHeight;
        }

        // Final right-shift to remove fixed-point fractional bits
        int totalPixels = outputWidth * outputHeight;
        for (int i = 0; i < totalPixels; i++)
        {
            destination[i] = (destination[i] + Half) >> FracBits;
        }
    }

    /// <summary>
    /// Applies inverse 9-7 lifting to a single column using fixed-point arithmetic.
    /// Copies column data into a contiguous buffer for cache-friendly sequential access,
    /// performs all lifting steps there, then writes results back to the strided array.
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
            buffer[n] = data[(n * stride) + x];
        }

        // Scale: even *= K, odd *= InvK (fixed-point multiply)
        for (int n = 0; n < evenCount; n++)
        {
            buffer[n * 2] = (int)((((long)buffer[n * 2] * KFix) + Half) >> FracBits);
        }

        for (int n = 0; n < oddCount; n++)
        {
            buffer[(n * 2) + 1] = (int)((((long)buffer[(n * 2) + 1] * InvKFix) + Half) >> FracBits);
        }

        // Step 4: undo update 2
        buffer[0] -= (int)((((long)DeltaFix * (buffer[1] + buffer[1])) + Half) >> FracBits);
        for (int n = 1; n < evenCount - 1; n++)
        {
            buffer[n * 2] -= (int)((((long)DeltaFix * (buffer[(n * 2) - 1] + buffer[(n * 2) + 1])) + Half) >> FracBits);
        }

        if (evenCount > 1)
        {
            int lastEven = (evenCount - 1) * 2;
            int dCurr = (evenCount - 1 < oddCount) ? buffer[lastEven + 1] : buffer[height - 2];
            buffer[lastEven] -= (int)((((long)DeltaFix * (buffer[lastEven - 1] + dCurr)) + Half) >> FracBits);
        }

        // Step 3: undo predict 2
        for (int n = 0; n < oddCount - 1; n++)
        {
            buffer[(n * 2) + 1] -= (int)((((long)GammaFix * (buffer[n * 2] + buffer[(n + 1) * 2])) + Half) >> FracBits);
        }

        {
            int lastOdd = ((oddCount - 1) * 2) + 1;
            int eNext = (oddCount < evenCount) ? buffer[oddCount * 2] : buffer[(oddCount - 1) * 2];
            buffer[lastOdd] -= (int)((((long)GammaFix * (buffer[(oddCount - 1) * 2] + eNext)) + Half) >> FracBits);
        }

        // Step 2: undo update 1
        buffer[0] -= (int)((((long)BetaFix * (buffer[1] + buffer[1])) + Half) >> FracBits);
        for (int n = 1; n < evenCount - 1; n++)
        {
            buffer[n * 2] -= (int)((((long)BetaFix * (buffer[(n * 2) - 1] + buffer[(n * 2) + 1])) + Half) >> FracBits);
        }

        if (evenCount > 1)
        {
            int lastEven = (evenCount - 1) * 2;
            int dCurr = (evenCount - 1 < oddCount) ? buffer[lastEven + 1] : buffer[height - 2];
            buffer[lastEven] -= (int)((((long)BetaFix * (buffer[lastEven - 1] + dCurr)) + Half) >> FracBits);
        }

        // Step 1: undo predict 1
        for (int n = 0; n < oddCount - 1; n++)
        {
            buffer[(n * 2) + 1] -= (int)((((long)AlphaFix * (buffer[n * 2] + buffer[(n + 1) * 2])) + Half) >> FracBits);
        }

        {
            int lastOdd = ((oddCount - 1) * 2) + 1;
            int eNext = (oddCount < evenCount) ? buffer[oddCount * 2] : buffer[(oddCount - 1) * 2];
            buffer[lastOdd] -= (int)((((long)AlphaFix * (buffer[(oddCount - 1) * 2] + eNext)) + Half) >> FracBits);
        }

        // Scatter back to column
        for (int n = 0; n < height; n++)
        {
            data[(n * stride) + x] = buffer[n];
        }
    }

    /// <summary>
    /// Applies inverse 9-7 lifting to a single row using fixed-point arithmetic.
    /// Row data is already contiguous in memory, so no temporary buffer is needed.
    /// Boundary conditions are peeled out of the main loops to avoid per-iteration branches.
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

        // Scale: even *= K, odd *= InvK (fixed-point multiply)
        for (int n = 0; n < evenCount; n++)
        {
            data[offset + (n * 2)] = (int)((((long)data[offset + (n * 2)] * KFix) + Half) >> FracBits);
        }

        for (int n = 0; n < oddCount; n++)
        {
            data[offset + (n * 2) + 1] = (int)((((long)data[offset + (n * 2) + 1] * InvKFix) + Half) >> FracBits);
        }

        // Undo update 2: boundary-peeled
        data[offset] -= (int)((((long)DeltaFix * (data[offset + 1] + data[offset + 1])) + Half) >> FracBits);
        for (int n = 1; n < evenCount - 1; n++)
        {
            data[offset + (n * 2)] -= (int)((((long)DeltaFix * (data[offset + (n * 2) - 1] + data[offset + (n * 2) + 1])) + Half) >> FracBits);
        }

        if (evenCount > 1)
        {
            int lastEvenIdx = offset + ((evenCount - 1) * 2);
            int dCurr = (evenCount - 1 < oddCount) ? data[lastEvenIdx + 1] : data[offset + width - 2];
            data[lastEvenIdx] -= (int)((((long)DeltaFix * (data[lastEvenIdx - 1] + dCurr)) + Half) >> FracBits);
        }

        // Undo predict 2
        for (int n = 0; n < oddCount - 1; n++)
        {
            data[offset + (n * 2) + 1] -= (int)((((long)GammaFix * (data[offset + (n * 2)] + data[offset + ((n + 1) * 2)])) + Half) >> FracBits);
        }

        {
            int lastOddIdx = offset + ((oddCount - 1) * 2) + 1;
            int eNext = (oddCount < evenCount) ? data[offset + (oddCount * 2)] : data[offset + ((oddCount - 1) * 2)];
            data[lastOddIdx] -= (int)((((long)GammaFix * (data[offset + ((oddCount - 1) * 2)] + eNext)) + Half) >> FracBits);
        }

        // Undo update 1: boundary-peeled
        data[offset] -= (int)((((long)BetaFix * (data[offset + 1] + data[offset + 1])) + Half) >> FracBits);
        for (int n = 1; n < evenCount - 1; n++)
        {
            data[offset + (n * 2)] -= (int)((((long)BetaFix * (data[offset + (n * 2) - 1] + data[offset + (n * 2) + 1])) + Half) >> FracBits);
        }

        if (evenCount > 1)
        {
            int lastEvenIdx = offset + ((evenCount - 1) * 2);
            int dCurr = (evenCount - 1 < oddCount) ? data[lastEvenIdx + 1] : data[offset + width - 2];
            data[lastEvenIdx] -= (int)((((long)BetaFix * (data[lastEvenIdx - 1] + dCurr)) + Half) >> FracBits);
        }

        // Undo predict 1
        for (int n = 0; n < oddCount - 1; n++)
        {
            data[offset + (n * 2) + 1] -= (int)((((long)AlphaFix * (data[offset + (n * 2)] + data[offset + ((n + 1) * 2)])) + Half) >> FracBits);
        }

        {
            int lastOddIdx = offset + ((oddCount - 1) * 2) + 1;
            int eNext = (oddCount < evenCount) ? data[offset + (oddCount * 2)] : data[offset + ((oddCount - 1) * 2)];
            data[lastOddIdx] -= (int)((((long)AlphaFix * (data[offset + ((oddCount - 1) * 2)] + eNext)) + Half) >> FracBits);
        }
    }
}
