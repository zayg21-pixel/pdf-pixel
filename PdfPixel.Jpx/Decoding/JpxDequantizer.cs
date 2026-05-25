using PdfPixel.Jpx.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Inverse quantization utilities for JPEG2000 subband coefficients.
/// Converts sign-magnitude values from the Tier-1 entropy decoder into
/// integer (reversible 5-3) or fixed-point (irreversible 9-7) representation.
/// Follows the reference approach from CoreJ2K's StdDequantizer.
/// </summary>
internal static class JpxDequantizer
{
    /// <summary>
    /// Parameters for irreversible (9-7) dequantization: the right-shift amount
    /// and the mantissa multiplier <c>(2048 + mantissa)</c> from the QCD/QCC step size.
    /// </summary>
    internal readonly struct IrreversibleStepParams
    {
        /// <summary>
        /// Right-shift applied to the magnitude before the mantissa multiply.
        /// </summary>
        public readonly int Shift;

        /// <summary>
        /// Fixed-point mantissa factor: <c>2048 + m</c> where <c>m</c> is the 11-bit mantissa.
        /// </summary>
        public readonly int MantissaFactor;

        public IrreversibleStepParams(int shift, int mantissaFactor)
        {
            Shift = shift;
            MantissaFactor = mantissaFactor;
        }
    }

    /// <summary>
    /// Computes the right-shift for reversible (5-3) dequantization.
    /// For reversible quantization <c>shiftBits = 31 - magBits</c> where
    /// <c>magBits = guardBits + exponent - 1</c>. No mantissa or bit-depth scaling is needed
    /// because the 5-3 transform preserves integer values exactly.
    /// </summary>
    /// <param name="quantization">Quantization parameters from QCD/QCC marker.</param>
    /// <param name="stepIndex">Subband index into the step-size table (0 = LL).</param>
    /// <returns>The number of bits to right-shift the sign-magnitude coefficient.</returns>
    public static int ComputeReversibleShift(JpxQuantization quantization, int stepIndex)
    {
        int guardBits = quantization.GuardBits;

        int exponent;
        if (quantization.QuantizationType == 0)
        {
            // No quantization (reversible): each entry is just an exponent
            if (quantization.StepSizes != null && stepIndex < quantization.StepSizes.Length)
            {
                exponent = (quantization.StepSizes[stepIndex] >> 11) & 0x1F;
            }
            else
            {
                // Fallback: derive from LL exponent using correct level-based offset
                // (same logic as scalar-derived: exponent decreases by 1 per resolution level,
                // not by 1 per subband index)
                int llExponent = (quantization.StepSizes?.Length > 0)
                    ? (quantization.StepSizes[0] >> 11) & 0x1F
                    : 8;
                int levelOffset = (stepIndex == 0) ? 0 : (((stepIndex - 1) / 3) + 1);
                exponent = llExponent - levelOffset;
            }
        }
        else if (quantization.QuantizationType == 1)
        {
            // Scalar derived: single base entry, derive others
            int baseExponent = (quantization.StepSizes[0] >> 11) & 0x1F;
            // Derived exponent decreases by 1 per resolution level
            // QCD index 0=LL, then groups of 3 per level
            int levelOffset = (stepIndex == 0) ? 0 : (((stepIndex - 1) / 3) + 1);
            exponent = baseExponent - levelOffset;
        }
        else
        {
            // Scalar expounded: each subband has its own entry
            if (quantization.StepSizes != null && stepIndex < quantization.StepSizes.Length)
            {
                exponent = (quantization.StepSizes[stepIndex] >> 11) & 0x1F;
            }
            else
            {
                exponent = 8;
            }
        }

        int magBits = guardBits + exponent - 1;
        int shiftBits = 31 - magBits;

        return Math.Max(shiftBits, 0);
    }

    /// <summary>
    /// Computes the dequantization parameters for an irreversible (9-7) subband.
    /// The true reconstructed value per the reference implementation (CoreJ2K StdDequantizer) is:
    /// <c>value = magnitude * (1 + m/2048) * 2^(rb + gain + guardBits - 32)</c>
    /// In <paramref name="fracBits"/> fixed-point the shift on magnitude is
    /// <c>32 - bitDepth - subbandGain - guardBits - fracBits</c>,
    /// followed by multiplication with <c>(2048 + mantissa)</c> and <c>&gt;&gt; 11</c>.
    /// </summary>
    /// <param name="quantization">Quantization parameters from QCD/QCC marker.</param>
    /// <param name="stepIndex">Subband index into the step-size table (0 = LL).</param>
    /// <param name="subbandGain">Subband analysis gain exponent (0 for LL, 1 for HL/LH, 2 for HH).</param>
    /// <param name="bitDepth">Component bit depth from the SIZ marker.</param>
    /// <param name="fracBits">Number of fractional bits in the fixed-point representation.</param>
    /// <returns>The shift and mantissa factor for dequantization.</returns>
    public static IrreversibleStepParams ComputeIrreversibleParams(
        JpxQuantization quantization,
        int stepIndex,
        int subbandGain,
        int bitDepth,
        int fracBits)
    {
        if (quantization?.StepSizes == null || stepIndex >= quantization.StepSizes.Length)
        {
            return new IrreversibleStepParams(31 - fracBits, 2048);
        }

        int guardBits = quantization.GuardBits;
        ushort encoded = quantization.StepSizes[stepIndex];
        int mantissa = encoded & 0x7FF;

        // Derived from CoreJ2K: shift = 32 - bitDepth - subbandGain - guardBits - fracBits
        int shift = 32 - bitDepth - subbandGain - guardBits - fracBits;

        return new IrreversibleStepParams(Math.Max(shift, 0), 2048 + mantissa);
    }

    /// <summary>
    /// Dequantizes a sign-magnitude coefficient using a simple right-shift (reversible path).
    /// The Tier-1 decoder stores the sign in bit 31 and magnitude in bits 30..0.
    /// </summary>
    /// <param name="coefficient">Sign-magnitude coefficient from the Tier-1 decoder.</param>
    /// <param name="shiftBits">Right-shift amount from <see cref="ComputeReversibleShift"/>.</param>
    /// <returns>The dequantized integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DequantizeReversible(int coefficient, int shiftBits)
    {
        if (coefficient >= 0)
        {
            return coefficient >> shiftBits;
        }

        // Negative: sign bit is set, magnitude in lower 31 bits
        return -((coefficient & 0x7FFFFFFF) >> shiftBits);
    }

    /// <summary>
    /// Dequantizes a sign-magnitude coefficient into fixed-point representation,
    /// applying both the right-shift and the quantization step mantissa factor (irreversible path).
    /// </summary>
    /// <param name="coefficient">Sign-magnitude coefficient from the Tier-1 decoder.</param>
    /// <param name="stepParams">Dequantization parameters from <see cref="ComputeIrreversibleParams"/>.</param>
    /// <returns>The dequantized value in fixed-point representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DequantizeIrreversible(int coefficient, in IrreversibleStepParams stepParams)
    {
        if (coefficient == 0)
        {
            return 0;
        }

        int sign;
        int magnitude;

        if (coefficient >= 0)
        {
            sign = 1;
            magnitude = coefficient;
        }
        else
        {
            sign = -1;
            magnitude = coefficient & 0x7FFFFFFF;
        }

        // Right-shift to fixed-point precision, then apply mantissa factor
        var result = (int)(((long)(magnitude >> stepParams.Shift) * stepParams.MantissaFactor) >> 11);

        return sign * result;
    }
}
