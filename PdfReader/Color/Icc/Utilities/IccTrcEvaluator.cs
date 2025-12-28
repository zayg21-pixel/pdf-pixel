using PdfReader.Color.Icc.Model;
using PdfReader.Functions;
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
                return MathF.Pow(x, trc.Gamma);
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
                return MathF.Pow(x, g);
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
                return MathF.Pow(a * x + b, g);
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
                return x < breakpoint ? c : MathF.Pow(a * x + b, g) + c;
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
                return x < d ? c * x : MathF.Pow(a * x + b, g);
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
                return x < d ? c * x + f : MathF.Pow(a * x + b, g) + e;
            }
            default:
            {
                return x;
            }
        }
    }
}
