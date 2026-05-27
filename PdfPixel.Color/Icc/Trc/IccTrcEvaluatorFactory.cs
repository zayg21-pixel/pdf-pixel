using PdfPixel.Color.Functions;
using PdfPixel.Color.Icc.Model;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc.Trc;

/// <summary>
/// Factory for creating IIccTrcEvaluator instances for ICC TRC definitions.
/// </summary>
internal static class IccTrcEvaluatorFactory
{
    /// <summary>
    /// Returns an evaluator for the given TRC definition.
    /// </summary>
    /// <param name="trc">TRC definition (gamma, sampled, parametric, or null).</param>
    /// <returns>Evaluator instance for the curve.</returns>
    public static IIccTrcEvaluator Create(IccTrc? trc)
    {
        if (trc == null || trc.Type == IccTrcType.None)
        {
            return IdentityTrcEvaluator.Instance;
        }

        switch (trc.Type)
        {
            case IccTrcType.Gamma:
                return new GammaTrcEvaluator(trc.Gamma);

            case IccTrcType.Sampled:
                return new SampledTrcEvaluator(trc.Samples);

            case IccTrcType.Parametric:
                return CreateParametric(trc);

            default:
                return IdentityTrcEvaluator.Instance;
        }
    }

    private static IIccTrcEvaluator CreateParametric(IccTrc trc)
    {
        IccTrcParametricType type = trc.ParametricType;
        IccTrcParameters? p = trc.TrcParameters;

        if (p == null)
        {
            return IdentityTrcEvaluator.Instance;
        }

        switch (type)
        {
            case IccTrcParametricType.Gamma:
                return new GammaTrcEvaluator(p);

            case IccTrcParametricType.PowerWithOffset:
                return new PowerWithOffsetTrcEvaluator(p);

            case IccTrcParametricType.PowerWithOffsetAndC:
                return new PowerWithOffsetAndCTrcEvaluator(p);

            case IccTrcParametricType.PowerWithLinearSegment:
                return new PowerWithLinearSegmentTrcEvaluator(p);

            case IccTrcParametricType.PowerWithLinearSegmentAndOffset:
                return new PowerWithLinearSegmentAndOffsetTrcEvaluator(p);

            default:
                return IdentityTrcEvaluator.Instance;
        }
    }

    private sealed class IdentityTrcEvaluator : IIccTrcEvaluator
    {
        public static readonly IdentityTrcEvaluator Instance = new();

        private IdentityTrcEvaluator()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x) => x;
    }


    private sealed class GammaTrcEvaluator : IIccTrcEvaluator
    {
        private readonly FastPowSeriesDegree3 _pow;

        public GammaTrcEvaluator(float gamma) => _pow = new FastPowSeriesDegree3(gamma);

        public GammaTrcEvaluator(IccTrcParameters parameters) => _pow = new FastPowSeriesDegree3(parameters.Gamma);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x) => _pow.Evaluate(x);
    }

    private sealed class SampledTrcEvaluator : IIccTrcEvaluator
    {
        private readonly float[] _samples;
        private readonly float _scale;

        public SampledTrcEvaluator(float[]? samples)
        {
            float[] src = samples ?? System.Array.Empty<float>();
            _samples = src;
            _scale = (_samples.Length > 1) ? _samples.Length - 1 : 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            if (_samples.Length == 0)
            {
                return x;
            }

            float u = x * _scale;
            var i1 = (int)u;

            if (i1 < 0)
            {
                return 0f;
            }

            if (i1 >= _samples.Length - 1)
            {
                return (i1 >= _samples.Length) ? 1f : _samples[i1];
            }

            float t = u - i1;
            int i0 = (i1 > 0) ? i1 - 1 : 0;
            int i2 = i1 + 1;
            int i3 = (i2 < _samples.Length - 1) ? i2 + 1 : _samples.Length - 1;

            float p0 = _samples[i0];
            float p1 = _samples[i1];
            float p2 = _samples[i2];
            float p3 = _samples[i3];

            float t2 = t * t;
            float t3 = t2 * t;
            float a0 = (-0.5f * p0) + (1.5f * p1) - (1.5f * p2) + (0.5f * p3);
            float a1 = p0 - (2.5f * p1) + (2f * p2) - (0.5f * p3);
            float a2 = (-0.5f * p0) + (0.5f * p2);
            return (a0 * t3) + (a1 * t2) + (a2 * t) + p1;
        }
    }

    private sealed class PowerWithLinearSegmentTrcEvaluator : IIccTrcEvaluator
    {
        private readonly FastPowSeriesDegree3 _pow;
        private readonly IccTrcParameters _p;

        public PowerWithLinearSegmentTrcEvaluator(IccTrcParameters p)
        {
            _pow = new FastPowSeriesDegree3(p.Gamma);
            _p = p;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            return (x < _p.Breakpoint)
                ? _p.ConstantC * x
                : _pow.Evaluate((_p.Scale * x) + _p.Offset);
        }
    }

    private sealed class PowerWithLinearSegmentAndOffsetTrcEvaluator : IIccTrcEvaluator
    {
        private readonly FastPowSeriesDegree3 _pow;
        private readonly IccTrcParameters _p;

        public PowerWithLinearSegmentAndOffsetTrcEvaluator(IccTrcParameters p)
        {
            _pow = new FastPowSeriesDegree3(p.Gamma);
            _p = p;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            return (x < _p.Breakpoint)
                ? (_p.ConstantC * x) + _p.LinearOffset
                : _pow.Evaluate((_p.Scale * x) + _p.Offset) + _p.PowerOffset;
        }
    }

    private sealed class PowerWithOffsetAndCTrcEvaluator : IIccTrcEvaluator
    {
        private readonly FastPowSeriesDegree3 _pow;
        private readonly IccTrcParameters _p;

        public PowerWithOffsetAndCTrcEvaluator(IccTrcParameters p)
        {
            _pow = new FastPowSeriesDegree3(p.Gamma);
            _p = p;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            return (x < _p.Breakpoint)
                ? _p.ConstantC
                : _pow.Evaluate((_p.Scale * x) + _p.Offset) + _p.ConstantC;
        }
    }

    private sealed class PowerWithOffsetTrcEvaluator : IIccTrcEvaluator
    {
        private readonly FastPowSeriesDegree3 _pow;
        private readonly IccTrcParameters _p;

        public PowerWithOffsetTrcEvaluator(IccTrcParameters p)
        {
            _pow = new FastPowSeriesDegree3(p.Gamma);
            _p = p;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            return (x < _p.Breakpoint)
                ? 0
                : _pow.Evaluate((_p.Scale * x) + _p.Offset);
        }
    }
}
