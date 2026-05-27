using PdfPixel.Color.Icc.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc;

public static partial class IccProfileAnalyzer
{
    private const float StandardSrgbGamma = 2.2f;
    private const int TrcComparisonPoints = 32;
    private const float TrcTolerance = 0.02f;

    private static bool IsTrcSimilar(IccTrc trc1, IccTrc trc2, int points, float tolerance)
    {
        if (trc1 == null || trc2 == null)
        {
            return trc1 == trc2;
        }

        for (int i = 0; i < points; i++)
        {
            float x = i / (float)(points - 1);

            if (Math.Abs(trc1.Evaluator.Evaluate(x) - trc2.Evaluator.Evaluate(x)) > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPassthroughTrc(IccTrc[] trcs, int channelCount)
    {
        for (int i = 0; i < channelCount; i++)
        {
            if (!IsIdentityTrc(trcs[i]))
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsIdentityTrc(IccTrc trc)
    {
        if (trc == null)
        {
            return true;
        }

        return trc.Type switch
        {
            IccTrcType.None => true,
            IccTrcType.Sampled => IsLinearSampledCurve(trc.Samples),
            IccTrcType.Gamma => Math.Abs(trc.Gamma - 1.0f) < 1e-6f,
            IccTrcType.Parametric => IsIdentityParametric(trc.ParametricType, trc.Parameters),
            _ => false
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLinearSampledCurve(float[]? samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return true;
        }

        int length = samples.Length;

        if (Math.Abs(samples[0]) > 1e-6f || Math.Abs(samples[length - 1] - 1.0f) > 1e-6f)
        {
            return false;
        }

        float lastIndex = length - 1;
        for (int i = 1; i < length - 1; i++)
        {
            if (Math.Abs(samples[i] - i / lastIndex) > 1e-5f)
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentityParametric(IccTrcParametricType type, float[]? parameters)
    {
        if (parameters == null)
        {
            return true;
        }

        return type switch
        {
            IccTrcParametricType.Gamma => parameters.Length >= 1 && Math.Abs(parameters[0] - 1.0f) < 1e-6f,
            _ => false
        };
    }
}
