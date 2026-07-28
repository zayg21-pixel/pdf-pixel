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
    /// Quantization style signalling one step size that every subband derives its own from
    /// (ITU-T T.800 Table A.28).
    /// </summary>
    private const int ScalarDerivedQuantization = 1;

    /// <summary>
    /// Magnitude scale used when the QCD/QCC marker carries no usable step size.
    /// </summary>
    private const float FallbackScale = 1f / 2147483648f;

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
    /// Computes the factor that converts an irreversible (9-7) subband magnitude into its
    /// reconstructed sample value. Per the reference implementation (CoreJ2K StdDequantizer):
    /// <c>value = magnitude * (1 + m/2048) * 2^(rb + gain + guardBits - 32)</c>, which folds
    /// into a single multiplier of <c>(2048 + m) * 2^(rb + gain + guardBits - 43)</c>.
    /// </summary>
    /// <param name="quantization">Quantization parameters from QCD/QCC marker.</param>
    /// <param name="stepIndex">Subband index into the step-size table (0 = LL).</param>
    /// <param name="subbandGain">Subband analysis gain exponent (0 for LL, 1 for HL/LH, 2 for HH).</param>
    /// <param name="bitDepth">Component bit depth from the SIZ marker.</param>
    /// <returns>The scale to multiply the coefficient magnitude by.</returns>
    public static float ComputeIrreversibleScale(
        JpxQuantization quantization,
        int stepIndex,
        int subbandGain,
        int bitDepth)
    {
        if (quantization?.StepSizes == null || quantization.StepSizes.Length == 0)
        {
            return FallbackScale;
        }

        // Scalar derived quantization signals a single step size, for the LL band, and every
        // other band derives its own from it (ITU-T T.800 E.1.1). Scalar expounded signals one
        // per subband, so each reads its own entry.
        bool isDerived = quantization.QuantizationType == ScalarDerivedQuantization;
        int index = isDerived ? 0 : stepIndex;

        if (index >= quantization.StepSizes.Length)
        {
            return FallbackScale;
        }

        int guardBits = quantization.GuardBits;
        ushort encoded = quantization.StepSizes[index];
        int mantissa = encoded & 0x7FF;

        return (2048 + mantissa) * MathF.Pow(2f, bitDepth + subbandGain + guardBits - 43);
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
        // Sign extends to a full mask, which turns the negation into a conditional-free
        // complement-and-increment rather than a branch on unpredictable data.
        int sign = coefficient >> 31;
        int magnitude = (coefficient & 0x7FFFFFFF) >> shiftBits;

        return (magnitude ^ sign) - sign;
    }

    /// <summary>
    /// Dequantizes a sign-magnitude coefficient into its reconstructed sample value
    /// (irreversible path). The Tier-1 decoder stores the sign in bit 31 and the
    /// magnitude in bits 30..0.
    /// </summary>
    /// <param name="coefficient">Sign-magnitude coefficient from the Tier-1 decoder.</param>
    /// <param name="scale">Subband scale from <see cref="ComputeIrreversibleScale"/>.</param>
    /// <returns>The dequantized sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DequantizeIrreversible(int coefficient, float scale)
    {
        float magnitude = (coefficient & 0x7FFFFFFF) * scale;

        return (coefficient < 0) ? -magnitude : magnitude;
    }
}
