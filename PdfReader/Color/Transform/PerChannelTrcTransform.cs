using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Utilities;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform;

/// <summary>
/// Implements <see cref="IColorTransform"/> using direct ICC TRC evaluation for per-channel color mapping.
/// Uses <see cref="IIccTrcEvaluator"/> for efficient curve evaluation without intermediate LUT generation.
/// Optimized for high-precision color transformations with minimal memory overhead.
/// </summary>
internal sealed class PerChannelTrcTransform : IColorTransform
{
    private readonly IIccTrcVectorEvaluator _vectorEvaluator;
    private readonly int _channelCount;
    private readonly bool _isPassthrough;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerChannelTrcTransform"/> class from ICC transfer curves.
    /// </summary>
    /// <param name="trcs">Array of ICC transfer curves. Maximum 4 channels supported.</param>
    public PerChannelTrcTransform(params IccTrc[] trcs)
    {
        if (trcs == null || trcs.Length == 0)
        {
            _channelCount = 0;
            _isPassthrough = true;
            return;
        }

        _channelCount = Math.Min(trcs.Length, 4);
        _isPassthrough = IsPassthroughTransform(trcs, _channelCount);

        if (!_isPassthrough)
        {
            _vectorEvaluator = IccTrcVectorEvaluatorFactory.Create(trcs);
        }
    }

    public bool IsIdentity => _isPassthrough;

    /// <summary>
    /// Transforms the input color vector by evaluating each channel through its corresponding TRC.
    /// </summary>
    /// <param name="color">The input color vector (normalized 0-1 range expected).</param>
    /// <returns>The transformed color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        if (_isPassthrough)
        {
            return color;
        }

        return _vectorEvaluator.Evaluate(color);
    }

    /// <summary>
    /// Determines if the transform is effectively a passthrough (all TRCs are identity or null).
    /// </summary>
    /// <param name="trcs">Array of TRCs to check.</param>
    /// <param name="channelCount">Number of channels to check.</param>
    /// <returns>True if all TRCs represent identity transforms.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPassthroughTransform(IccTrc[] trcs, int channelCount)
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

    /// <summary>
    /// Checks if a TRC represents an identity transform.
    /// Focus on practical cases: null TRCs and linear [0,1] sampled curves.
    /// </summary>
    /// <param name="trc">TRC to check.</param>
    /// <returns>True if the TRC is null, represents no transform, or is a linear [0,1] mapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentityTrc(IccTrc trc)
    {
        if (trc == null)
        {
            return true;
        }

        switch (trc.Type)
        {
            case IccTrcType.None:
                return true;
                
            case IccTrcType.Sampled:
                // Check for linear [0,1] sampled curves - very common in ICC profiles
                return IsLinearSampledCurve(trc.Samples);
                
            case IccTrcType.Gamma:
                // Identity if gamma is very close to 1.0 (rare but possible)
                return Math.Abs(trc.Gamma - 1.0f) < 1e-6f;
                
            case IccTrcType.Parametric:
                // Check for parametric identity (rare)
                return IsIdentityParametric(trc.ParametricType, trc.Parameters);
                
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks if a sampled curve represents a linear [0,1] mapping.
    /// This is very common in ICC profiles for identity transformations.
    /// </summary>
    /// <param name="samples">Sample array to check.</param>
    /// <returns>True if samples represent linear 0→1 mapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLinearSampledCurve(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return true; // Treat empty/null as identity
        }

        int length = samples.Length;
        
        // Check first and last values for [0,1] range
        if (Math.Abs(samples[0]) > 1e-6f || Math.Abs(samples[length - 1] - 1.0f) > 1e-6f)
        {
            return false;
        }

        // Check linearity: sample[i] should equal i/(length-1)
        float lastIndex = length - 1;
        for (int i = 1; i < length - 1; i++) // Skip first/last already checked
        {
            float expected = i / lastIndex;
            if (Math.Abs(samples[i] - expected) > 1e-5f) // Slightly looser tolerance for accumulated error
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if parametric curve parameters represent an identity transform.
    /// </summary>
    /// <param name="type">Parametric curve type.</param>
    /// <param name="parameters">Curve parameters.</param>
    /// <returns>True if parameters represent identity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentityParametric(IccTrcParametricType type, float[] parameters)
    {
        if (parameters == null)
        {
            return true;
        }

        return type switch
        {
            IccTrcParametricType.Gamma => 
                parameters.Length >= 1 && Math.Abs(parameters[0] - 1.0f) < 1e-6f,
                
            // For other parametric types, we'd need to check specific parameter combinations
            // that result in identity transforms. For simplicity, assume non-identity.
            _ => false,
        };
    }
}