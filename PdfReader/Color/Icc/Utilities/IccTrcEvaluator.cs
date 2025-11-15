using PdfReader.Color.Icc.Model;
using System;

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
    public static float EvaluateTrc(IccTrc trc, float x)
    {
        if (trc == null)
        {
            return x;
        }

        if (trc.IsGamma)
        {
            return MathF.Pow(x, trc.Gamma);
        }

        if (trc.IsSampled)
        {
            float[] samples = trc.Samples;
            if (samples == null || samples.Length == 0)
            {
                return x; // Placeholder sampled curve – treat as linear.
            }

            float scaled = x * (samples.Length - 1);
            int index0 = (int)scaled;
            if (index0 >= samples.Length - 1)
            {
                return samples[samples.Length - 1];
            }
            int index1 = index0 + 1;
            float fraction = scaled - index0;
            float v0 = samples[index0];
            float v1 = samples[index1];
            return v0 + (v1 - v0) * fraction;
        }

        if (trc.IsParametric)
        {
            return ApplyParametric(trc.ParametricType, trc.Parameters, x);
        }

        // Unsupported or unknown kinds fall back to linear.
        return x;
    }

    /// <summary>
    /// Evaluate an ICC parametric curve type (0..4) with the provided parameters.
    /// Each case implements the piecewise formulas defined in the ICC specification.
    /// </summary>
    /// <param name="type">Parametric curve type (0..4).</param>
    /// <param name="parameters">Parameter array (length depends on type).</param>
    /// <param name="x">Normalized input value.</param>
    /// <returns>Normalized output value.</returns>
    public static float ApplyParametric(int type, float[] parameters, float x)
    {
        switch (type)
        {
            case 0:
            {
                if (parameters == null || parameters.Length < 1)
                {
                    return x;
                }
                float g = parameters[0];
                return MathF.Pow(x, g);
            }
            case 1:
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
            case 2:
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
            case 3:
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
            case 4:
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
