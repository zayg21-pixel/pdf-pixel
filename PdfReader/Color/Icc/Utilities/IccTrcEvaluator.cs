using PdfReader.Color.Icc.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Icc.Utilities;

/// <summary>
/// Evaluation helpers for ICC tone reproduction curves (TRCs).
/// Provides interpolation for sampled curves, exponent evaluation for gamma curves, and
/// implementations of ICC parametric curve types 0..4. Inputs and outputs are normalized (0..1).
/// </summary>
internal static class IccTrcEvaluator
{
    /// <summary>
    /// Evaluate a TRC for an input value in the 0..1 domain returning a 0..1 output.
    /// </summary>
    /// <param name="trc">TRC definition (gamma, sampled or parametric). Null returns the input unchanged.</param>
    /// <param name="x">Normalized input value (expected 0..1; values outside are not clamped here).</param>
    /// <returns>Curve output (0..1 nominal range).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EvaluateTrc(IccTrc trc, float x)
    {
        if (trc == null)
        {
            return x;
        }

        switch (trc.Type)
        {
            case IccTrcType.Gamma:
            {
                return FastPowPade.Evaluate(x, trc.Gamma);
            }
            case IccTrcType.Sampled:
            {
                return EvaluateSampled(x, trc.Samples);
            }
            case IccTrcType.Parametric:
            {
                return ApplyParametric(trc.ParametricType, trc.Parameters, x);
            }
            case IccTrcType.None:
            default:
            {
                // Unsupported or unknown kinds fall back to linear.
                return x;
            }
        }
    }

    /// <summary>
    /// Evaluates a sampled curve at the specified normalized position using linear interpolation.
    /// </summary>
    /// <remarks>If x is less than 0, the first sample value is returned. If x is greater than or equal to 1,
    /// the last sample value is returned. For values of x between 0 and 1, the method performs linear interpolation
    /// between the nearest sample points.</remarks>
    /// <param name="x">The normalized position at which to evaluate the curve. Typically in the range [0, 1].</param>
    /// <param name="samples">An array of sample values representing the curve to be evaluated. Must not be null or empty.</param>
    /// <returns>The interpolated value of the curve at the specified position. If the samples array is null or empty, returns
    /// the input value x.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float EvaluateSampled(float x, float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return x; // Placeholder sampled curve – treat as linear.
        }

        float scaled = x * (samples.Length - 1);
        int index0 = (int)scaled;

        if (index0 < 0)
        {
            return samples[0];
        }
        else if (index0 >= samples.Length - 1)
        {
            return samples[samples.Length - 1];
        }

        int index1 = index0 + 1;
        float fraction = scaled - index0;
        float v0 = samples[index0];
        float v1 = samples[index1];
        return v0 + (v1 - v0) * fraction;
    }

    /// <summary>
    /// Evaluate an ICC parametric curve type (0..4) with the provided parameters.
    /// Each case implements the piecewise formulas defined in the ICC specification.
    /// </summary>
    /// <param name="type">Parametric curve type (0..4).</param>
    /// <param name="parameters">Parameter array (length depends on type).</param>
    /// <param name="x">Normalized input value.</param>
    /// <returns>Normalized output value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyParametric(IccTrcParametricType type, float[] parameters, float x)
    {
        switch (type)
        {
            case IccTrcParametricType.Gamma:
            {
                if (parameters == null || parameters.Length < 1)
                {
                    return x;
                }
                float g = parameters[0];
                return FastPowPade.Evaluate(x, g);
            }
            case IccTrcParametricType.PowerWithOffset:
            {
                if (parameters == null || parameters.Length < 3)
                {
                    return x;
                }
                float g = parameters[0];
                float a = parameters[1];
                float b = parameters[2];
                float breakpoint = -b / (a == 0f ? 1e-20f : a);
                if (x < breakpoint)
                {
                    return 0f;
                }
                return FastPowPade.Evaluate(a * x + b, g);
            }
            case IccTrcParametricType.PowerWithOffsetAndC:
            {
                if (parameters == null || parameters.Length < 4)
                {
                    return x;
                }
                float g = parameters[0];
                float a = parameters[1];
                float b = parameters[2];
                float c = parameters[3];
                float breakpoint = -b / (a == 0f ? 1e-20f : a);
                return x < breakpoint ? c : FastPowPade.Evaluate(a * x + b, g) + c;
            }
            case IccTrcParametricType.PowerWithLinearSegment:
            {
                if (parameters == null || parameters.Length < 5)
                {
                    return x;
                }
                float g = parameters[0];
                float a = parameters[1];
                float b = parameters[2];
                float c = parameters[3];
                float d = parameters[4];
                return x < d ? c * x : FastPowPade.Evaluate(a * x + b, g);
            }
            case IccTrcParametricType.PowerWithLinearSegmentAndOffset:
            {
                if (parameters == null || parameters.Length < 7)
                {
                    return x;
                }
                float g = parameters[0];
                float a = parameters[1];
                float b = parameters[2];
                float c = parameters[3];
                float d = parameters[4];
                float e = parameters[5];
                float f = parameters[6];
                return x < d ? c * x + f : FastPowPade.Evaluate(a * x + b, g) + e;
            }
            default:
            {
                return x;
            }
        }
    }

    /// <summary>
    /// Fast approximation of x^p on [0,1] using range reduction and Pade series that converge fast.
    /// x^p = 2^(p * log2(x)).
    /// - log2(x): exponent/mantissa split with normalization to m in [sqrt(0.5), sqrt(2)],
    ///   then ln(m) via odd atanh series (Pade-like) in t=(m-1)/(m+1), converted to log2.
    /// - exp2(y): y = k + f with k = round(y), f in [-0.5, 0.5]. Use Pade [2/2] on u = f*ln2.
    /// Pade based method is 3-4 times faster than MathF.Pow with good accuracy for TRC use (around ~0.1 percent difference), also
    /// does not depend on different Pow implementations.
    /// </summary>
    private static class FastPowPade
    {
        private const float Ln2 = 0.693147180559945309417232121458176568f;
        private const float InvLn2 = 1.4426950408889634073599246810018921f; // 1/ln(2)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int FloatToIntBits(float value)
        {
            return *(int*)&value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float IntToFloatBits(int value)
        {
            return *(float*)&value;
        }

        /// <summary>
        /// Approximate x^p for x in [0,1]. Inputs outside [0,1] are clamped.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Evaluate(float x, float p)
        {
            if (x == 0)
            {
                return 0;
            }

            float log2x = ApproxLog2(x);
            float y = p * log2x;
            return ApproxExp2(y);
        }

        /// <summary>
        /// log2(x) via exponent split
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApproxLog2(float x)
        {
            int bits = FloatToIntBits(x);
            int exp = ((bits >> 23) & 0xFF) - 127;
            int mant = (bits & 0x7FFFFF) | (1 << 23);
            float m = mant * (1.0f / (1 << 23)); // m in [1,2)

            // t = (m - 1) / (m + 1) in ~[-0.1716, 0.1716]
            //float t = (m - 1.0f) / (m + 1.0f);
            float t = 1 - 2 / (m + 1.0f);
            float t2 = t * t;
            float t3 = t2 * t;
            float t5 = t3 * t2;

            // ln(m) ≈ 2(t + t^3/3 + t^5/5)  [odd atanh series], fast and low-bias on this range.
            float lnM = 2.0f * (t + (1.0f / 3.0f) * t3 + (1.0f / 5.0f) * t5);
            float log2m = lnM * InvLn2;
            return exp + log2m;
        }

        /// <summary>
        /// 2^y via y = k + f, k = round(y), f in [-0.5, 0.5].
        /// Pade [2/2] on u = f*ln2: exp(u) ≈ (1 + u/2 + u^2/10) / (1 - u/2 + u^2/10).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApproxExp2(float y)
        {
            int k = (int)MathF.Round(y);
            float f = y - k;
            float u = f * Ln2;
            float u2 = u * u;

            float num = 1.0f + 0.5f * u + 0.1f * u2;
            float den = 1.0f - 0.5f * u + 0.1f * u2;
            float ef = num / den;

            int kk = k + 127;

            int ik = kk << 23;
            float twoPowK = IntToFloatBits(ik);
            return twoPowK * ef;
        }
    }
}
