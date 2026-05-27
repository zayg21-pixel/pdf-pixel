using PdfPixel.Color.Icc;
using PdfPixel.Color.Icc.Model;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if !NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif

namespace PdfPixel.Color.Transform;

/// <summary>
/// Implements <see cref="IColorTransform"/> using direct ICC TRC evaluation for per-channel color mapping.
/// Uses sampled float arrays for efficient curve evaluation without intermediate LUT generation.
/// Optimized for high-precision color transformations with minimal memory overhead.
/// </summary>
public sealed class PerChannelTrcTransform : IColorTransform
{
    private const int MinSamplesCount = 1024; // Optimal value for 8-bit transform pipeline to eliminate rounding errors.

    private readonly int _channelCount;
    private readonly float[] _samples0;
    private readonly float[] _samples1;
    private readonly float[] _samples2;
    private readonly float[] _samples3;
    private readonly Vector4 _scale;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerChannelTrcTransform"/> class from ICC transfer curves.
    /// </summary>
    /// <param name="trcs">Array of ICC transfer curves. Maximum 4 channels supported.</param>
    public PerChannelTrcTransform(params IccTrc[] trcs)
    {
        if (trcs == null || trcs.Length == 0)
        {
            _channelCount = 0;
            IsIdentity = true;
            _samples0 = Array.Empty<float>();
            _samples1 = Array.Empty<float>();
            _samples2 = Array.Empty<float>();
            _samples3 = Array.Empty<float>();
            _scale = Vector4.One;
            return;
        }

        _channelCount = Math.Min(trcs.Length, 4);
        IsIdentity = IccProfileAnalyzer.IsPassthroughTrc(trcs, _channelCount);

        if (IsIdentity)
        {
            _samples0 = Array.Empty<float>();
            _samples1 = Array.Empty<float>();
            _samples2 = Array.Empty<float>();
            _samples3 = Array.Empty<float>();
            _scale = Vector4.One;
            return;
        }

        // Always use sampled version of all curves
        var samples = new float[_channelCount][];
        for (int i = 0; i < _channelCount; i++)
        {
            IccTrc trc = trcs[i];
            var channelSamples = new float[MinSamplesCount];

            for (int j = 0; j < channelSamples.Length; j++)
            {
                float t = j / (float)(channelSamples.Length - 1);
                channelSamples[j] = trc.Evaluator.Evaluate(t);
            }

            samples[i] = channelSamples;
        }

        _samples0 = samples[0];
        _samples1 = (_channelCount > 1) ? samples[1] : [];
        _samples2 = (_channelCount > 2) ? samples[2] : [];
        _samples3 = (_channelCount > 3) ? samples[3] : [];

        _scale = _channelCount switch
        {
            1 => new Vector4(_samples0.Length - 1, 1f, 1f, 1f),
            2 => new Vector4(_samples0.Length - 1, _samples1.Length - 1, 1f, 1f),
            3 => new Vector4(_samples0.Length - 1, _samples1.Length - 1, _samples2.Length - 1, 1f),
            _ => new Vector4(_samples0.Length - 1, _samples1.Length - 1, _samples2.Length - 1, _samples3.Length - 1)
        };
    }

    /// <inheritdoc/>
    public bool IsIdentity { get; }

    /// <summary>
    /// Transforms the input color vector by evaluating each channel through its corresponding sampled TRC.
    /// </summary>
    /// <param name="color">The input color vector (normalized 0-1 range expected).</param>
    /// <returns>The transformed color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        if (IsIdentity)
        {
            return color;
        }

        Vector4 scaled = color * _scale;

        switch (_channelCount)
        {
            case 1:
            {
                var idxX = (int)scaled.X;
                float r = LookupSample(_samples0, idxX);
                return new Vector4(r, 1f, 1f, 1f);
            }
            case 2:
            {
                var idxX = (int)scaled.X;
                var idxY = (int)scaled.Y;
                float r = LookupSample(_samples0, idxX);
                float g = LookupSample(_samples1, idxY);
                return new Vector4(r, g, 1f, 1f);
            }
            case 3:
            {
                var idxX = (int)scaled.X;
                var idxY = (int)scaled.Y;
                var idxZ = (int)scaled.Z;
                float r = LookupSample(_samples0, idxX);
                float g = LookupSample(_samples1, idxY);
                float b = LookupSample(_samples2, idxZ);
                return new Vector4(r, g, b, 1f);
            }
            default:
            {
                var idxX = (int)scaled.X;
                var idxY = (int)scaled.Y;
                var idxZ = (int)scaled.Z;
                var idxW = (int)scaled.W;
                float r = LookupSample(_samples0, idxX);
                float g = LookupSample(_samples1, idxY);
                float b = LookupSample(_samples2, idxZ);
                float a = LookupSample(_samples3, idxW);
                return new Vector4(r, g, b, a);
            }
        }
    }

    /// <summary>
    /// Performs a bounds-checked sample lookup with minimal branching.
    /// Uses (uint) cast trick to combine negative and overflow checks into a single comparison,
    /// and Unsafe.Add to bypass redundant JIT-emitted bounds checks after manual validation.
    /// </summary>
    /// <param name="samples">The sample array to look up.</param>
    /// <param name="index">The index to look up.</param>
    /// <returns>The sample value, clamped to 0 for negative indices and 1 for overflow.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LookupSample(float[] samples, int index)
    {
        if ((uint)index < (uint)samples.Length)
        {
#if NETSTANDARD2_0
            return Unsafe.Add(ref samples[0], index);
#else
            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(samples), index);
#endif
        }

        return (index < 0) ? 0f : 1f;
    }

}
