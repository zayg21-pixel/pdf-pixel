using PdfRender.Color.Icc.Model;
using PdfRender.Functions;
using System.Runtime.CompilerServices;

namespace PdfRender.Color.Icc.Utilities;

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
    public static IIccTrcEvaluator Create(IccTrc trc)
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
        var type = trc.ParametricType;
        var p = trc.TrcParameters;

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
        public static readonly IdentityTrcEvaluator Instance = new IdentityTrcEvaluator();

        private IdentityTrcEvaluator() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x) => x;
    }


    private sealed class GammaTrcEvaluator : IIccTrcEvaluator
    {
        private readonly FastPowSeriesDegree3 _pow;
        public GammaTrcEvaluator(float gamma)
        {
            _pow = new FastPowSeriesDegree3(gamma);
        }

        public GammaTrcEvaluator(IccTrcParameters parameters)
        {
            _pow = new FastPowSeriesDegree3(parameters.Gamma);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            return _pow.Evaluate(x);
        }
    }

    private sealed class SampledTrcEvaluator : IIccTrcEvaluator
    {
        private readonly float[] _samples;
        private readonly float _scale;

        public SampledTrcEvaluator(float[] samples)
        {
            var src = samples ?? System.Array.Empty<float>();
            _samples = src;
            _scale = _samples.Length > 1 ? _samples.Length - 1 : 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            if (_samples.Length == 0)
            {
                return x;
            }

            float scaled = x * _scale;
            int index = (int)scaled;

            if (index < 0)
            {
                return 0;
            }
            if (index >= _samples.Length)
            {
                return 1;
            }

            return _samples[index];
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
            return x < _p.Breakpoint
                ? _p.ConstantC * x
                : _pow.Evaluate(_p.Scale * x + _p.Offset);
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
            return x < _p.Breakpoint
                ? _p.ConstantC * x + _p.LinearOffset
                : _pow.Evaluate(_p.Scale * x + _p.Offset) + _p.PowerOffset;
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
            return x < _p.Breakpoint
                ? _p.ConstantC
                : _pow.Evaluate(_p.Scale * x + _p.Offset) + _p.ConstantC;
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
            return x < _p.Breakpoint
                ? 0
                : _pow.Evaluate(_p.Scale * x + _p.Offset);
        }
    }
}
