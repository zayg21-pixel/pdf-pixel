using PdfReader.Color.Icc.Model;
using PdfReader.Functions;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Icc.Utilities
{
    /// <summary>
    /// Interface for evaluating ICC TRC curves for multiple channels at once.
    /// </summary>
    internal interface IIccTrcVectorEvaluator
    {
        /// <summary>
        /// Evaluates the TRC for a vector of channel values.
        /// </summary>
        /// <param name="x">Input vector (per channel).</param>
        /// <returns>Evaluated vector (per channel).</returns>
        Vector4 Evaluate(Vector4 x);
    }

    /// <summary>
    /// Evaluator for gamma TRC using a vector of gamma values and FastPowSeriesDegree3Vector4.
    /// </summary>
    internal sealed class GammaTrcVectorEvaluator : IIccTrcVectorEvaluator
    {
        private readonly FastPowSeriesDegree3Vector4 _pow;

        public GammaTrcVectorEvaluator(float[] gamma)
        {
            if (gamma == null || gamma.Length == 0 || gamma.Length > 4)
            {
                throw new ArgumentException("gamma must be an array of 1 to 4 floats", nameof(gamma));
            }
            var g = new float[4];
            for (int i = 0; i < 4; i++)
            {
                g[i] = i < gamma.Length ? gamma[i] : 1.0f;
            }
            _pow = new FastPowSeriesDegree3Vector4(new Vector4(g[0], g[1], g[2], g[3]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            return _pow.Evaluate(x);
        }
    }

    /// <summary>
    /// Evaluator for PowerWithLinearSegment TRC for 4 channels at once.
    /// </summary>
    internal sealed class PowerWithLinearSegmentTrcVectorEvaluator : IIccTrcVectorEvaluator
    {
        private readonly FastPowSeriesDegree3Vector4 _pow;
        private readonly Vector4 _breakpoint;
        private readonly Vector4 _constantC;
        private readonly Vector4 _scale;
        private readonly Vector4 _offset;

        public PowerWithLinearSegmentTrcVectorEvaluator(IccTrcParameters[] parameters)
        {
            parameters = IccTrcVectorEvaluatorHelpers.FillParams(parameters);
            _breakpoint = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Breakpoint);
            _constantC = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.ConstantC);
            _scale = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Scale);
            _offset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Offset);
            var gammaVector = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Gamma);
            _pow = new FastPowSeriesDegree3Vector4(gammaVector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            var mask = new Vector4(
                x.X < _breakpoint.X ? 1f : 0f,
                x.Y < _breakpoint.Y ? 1f : 0f,
                x.Z < _breakpoint.Z ? 1f : 0f,
                x.W < _breakpoint.W ? 1f : 0f);

            if (mask == Vector4.One)
            {
                return _constantC * x;
            }
            if (mask == Vector4.Zero)
            {
                return _pow.Evaluate(_scale * x + _offset);
            }

            var linear = _constantC * x;
            var nonLinear = _pow.Evaluate(_scale * x + _offset);
            return mask * linear + (Vector4.One - mask) * nonLinear;
        }
    }

    /// <summary>
    /// Evaluator for sampled TRC for 4 channels at once.
    /// </summary>
    internal sealed class SampledTrcVectorEvaluator : IIccTrcVectorEvaluator
    {
        private readonly int _channelsCount;
        private readonly float[] _samples0;
        private readonly float[] _samples1;
        private readonly float[] _samples2;
        private readonly float[] _samples3;
        private readonly Vector4 _scale;

        public SampledTrcVectorEvaluator(float[][] samples)
        {
            if (samples == null || samples.Length == 0 || samples.Length > 4)
            {
                throw new ArgumentException("samples must be an array of 1 to 4 float[]", nameof(samples));
            }
            _channelsCount = samples.Length;
            _samples0 = samples.Length > 0 ? samples[0] : null;
            _samples1 = samples.Length > 1 ? samples[1] : null;
            _samples2 = samples.Length > 2 ? samples[2] : null;
            _samples3 = samples.Length > 3 ? samples[3] : null;

            switch (_channelsCount)
            {
                case 1:
                    _scale = new Vector4(_samples0.Length - 1, 1f, 1f, 1f);
                    break;
                case 2:
                    _scale = new Vector4(_samples0.Length - 1, _samples1.Length - 1, 1f, 1f);
                    break;
                case 3:
                    _scale = new Vector4(_samples0.Length - 1, _samples1.Length - 1, _samples2.Length - 1, 1f);
                    break;
                case 4:
                default:
                    _scale = new Vector4(_samples0.Length - 1, _samples1.Length - 1, _samples2.Length - 1, _samples3.Length - 1);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            Vector4 scaled = x * _scale;
            scaled = Vector4.Clamp(scaled, Vector4.Zero, _scale);

            switch (_channelsCount)
            {
                case 1:
                {
                    int idxX = (int)scaled.X;
                    float r = _samples0[idxX];
                    return new Vector4(r, 1f, 1f, 1f);
                }
                case 2:
                {
                    int idxX = (int)scaled.X;
                    int idxY = (int)scaled.Y;
                    float r = _samples0[idxX];
                    float g = _samples1[idxY];
                    return new Vector4(r, g, 1f, 1f);
                }
                case 3:
                {
                    int idxX = (int)scaled.X;
                    int idxY = (int)scaled.Y;
                    int idxZ = (int)scaled.Z;
                    float r = _samples0[idxX];
                    float g = _samples1[idxY];
                    float b = _samples2[idxZ];
                    return new Vector4(r, g, b, 1f);
                }
                case 4:
                default:
                {
                    int idxX = (int)scaled.X;
                    int idxY = (int)scaled.Y;
                    int idxZ = (int)scaled.Z;
                    int idxW = (int)scaled.W;
                    float r = _samples0[idxX];
                    float g = _samples1[idxY];
                    float b = _samples2[idxZ];
                    float a = _samples3[idxW];
                    return new Vector4(r, g, b, a);
                }
            }
        }
    }

    /// <summary>
    /// Evaluator for PowerWithLinearSegmentAndOffset TRC for 4 channels at once.
    /// </summary>
    internal sealed class PowerWithLinearSegmentAndOffsetTrcVectorEvaluator : IIccTrcVectorEvaluator
    {
        private readonly FastPowSeriesDegree3Vector4 _pow;
        private readonly Vector4 _breakpoint;
        private readonly Vector4 _constantC;
        private readonly Vector4 _scale;
        private readonly Vector4 _offset;
        private readonly Vector4 _powerOffset;
        private readonly Vector4 _linearOffset;

        public PowerWithLinearSegmentAndOffsetTrcVectorEvaluator(IccTrcParameters[] parameters)
        {
            parameters = IccTrcVectorEvaluatorHelpers.FillParams(parameters);
            _breakpoint = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Breakpoint);
            _constantC = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.ConstantC);
            _scale = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Scale);
            _offset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Offset);
            _powerOffset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.PowerOffset);
            _linearOffset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.LinearOffset);
            var gammaVector = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Gamma);
            _pow = new FastPowSeriesDegree3Vector4(gammaVector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            var mask = new Vector4(
                x.X < _breakpoint.X ? 1f : 0f,
                x.Y < _breakpoint.Y ? 1f : 0f,
                x.Z < _breakpoint.Z ? 1f : 0f,
                x.W < _breakpoint.W ? 1f : 0f);

            if (mask == Vector4.One)
            {
                return _constantC * x + _linearOffset;
            }
            if (mask == Vector4.Zero)
            {
                return _pow.Evaluate(_scale * x + _offset) + _powerOffset;
            }

            var linear = _constantC * x + _linearOffset;
            var nonLinear = _pow.Evaluate(_scale * x + _offset) + _powerOffset;
            return mask * linear + (Vector4.One - mask) * nonLinear;
        }
    }

    /// <summary>
    /// Evaluator for PowerWithOffsetAndC TRC for 4 channels at once.
    /// </summary>
    internal sealed class PowerWithOffsetAndCTrcVectorEvaluator : IIccTrcVectorEvaluator
    {
        private readonly FastPowSeriesDegree3Vector4 _pow;
        private readonly Vector4 _breakpoint;
        private readonly Vector4 _constantC;
        private readonly Vector4 _scale;
        private readonly Vector4 _offset;

        public PowerWithOffsetAndCTrcVectorEvaluator(IccTrcParameters[] parameters)
        {
            parameters = IccTrcVectorEvaluatorHelpers.FillParams(parameters);
            _breakpoint = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Breakpoint);
            _constantC = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.ConstantC);
            _scale = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Scale);
            _offset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Offset);
            var gammaVector = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Gamma);
            _pow = new FastPowSeriesDegree3Vector4(gammaVector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            var mask = new Vector4(
                x.X < _breakpoint.X ? 1f : 0f,
                x.Y < _breakpoint.Y ? 1f : 0f,
                x.Z < _breakpoint.Z ? 1f : 0f,
                x.W < _breakpoint.W ? 1f : 0f);

            if (mask == Vector4.One)
            {
                return _constantC;
            }
            if (mask == Vector4.Zero)
            {
                return _pow.Evaluate(_scale * x + _offset) + _constantC;
            }

            var linear = _constantC;
            var nonLinear = _pow.Evaluate(_scale * x + _offset) + _constantC;
            return mask * linear + (Vector4.One - mask) * nonLinear;
        }
    }

    /// <summary>
    /// Evaluator for PowerWithOffset TRC for 4 channels at once.
    /// </summary>
    internal sealed class PowerWithOffsetTrcVectorEvaluator : IIccTrcVectorEvaluator
    {
        private readonly FastPowSeriesDegree3Vector4 _pow;
        private readonly Vector4 _breakpoint;
        private readonly Vector4 _scale;
        private readonly Vector4 _offset;

        public PowerWithOffsetTrcVectorEvaluator(IccTrcParameters[] parameters)
        {
            parameters = IccTrcVectorEvaluatorHelpers.FillParams(parameters);
            _breakpoint = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Breakpoint);
            _scale = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Scale);
            _offset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Offset);
            var gammaVector = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Gamma);
            _pow = new FastPowSeriesDegree3Vector4(gammaVector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            var mask = new Vector4(
                x.X < _breakpoint.X ? 1f : 0f,
                x.Y < _breakpoint.Y ? 1f : 0f,
                x.Z < _breakpoint.Z ? 1f : 0f,
                x.W < _breakpoint.W ? 1f : 0f);

            if (mask == Vector4.One)
            {
                return Vector4.Zero;
            }
            if (mask == Vector4.Zero)
            {
                return _pow.Evaluate(_scale * x + _offset);
            }

            var linear = Vector4.Zero;
            var nonLinear = _pow.Evaluate(_scale * x + _offset);
            return mask * linear + (Vector4.One - mask) * nonLinear;
        }
    }

    /// <summary>
    /// Evaluator for per-channel ICC TRC using individual IIccTrcEvaluator fields for each channel.
    /// </summary>
    internal sealed class PerChannelTrcVectorEvaluator : IIccTrcVectorEvaluator
    {
        private readonly IIccTrcEvaluator _evaluator0;
        private readonly IIccTrcEvaluator _evaluator1;
        private readonly IIccTrcEvaluator _evaluator2;
        private readonly IIccTrcEvaluator _evaluator3;

        public PerChannelTrcVectorEvaluator(IccTrc[] trcs)
        {
            if (trcs == null || trcs.Length == 0 || trcs.Length > 4)
            {
                throw new ArgumentException("trcs must be an array of 1 to 4 IccTrc", nameof(trcs));
            }
            _evaluator0 = trcs.Length > 0 ? IccTrcEvaluatorFactory.Create(trcs[0]) : IccTrcEvaluatorFactory.Create(null);
            _evaluator1 = trcs.Length > 1 ? IccTrcEvaluatorFactory.Create(trcs[1]) : IccTrcEvaluatorFactory.Create(null);
            _evaluator2 = trcs.Length > 2 ? IccTrcEvaluatorFactory.Create(trcs[2]) : IccTrcEvaluatorFactory.Create(null);
            _evaluator3 = trcs.Length > 3 ? IccTrcEvaluatorFactory.Create(trcs[3]) : IccTrcEvaluatorFactory.Create(null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            float r = _evaluator0.Evaluate(x.X);
            float g = _evaluator1.Evaluate(x.Y);
            float b = _evaluator2.Evaluate(x.Z);
            float a = _evaluator3.Evaluate(x.W);
            return new Vector4(r, g, b, a);
        }
    }

    internal static class IccTrcVectorEvaluatorHelpers
    {
        public static IccTrcParameters IdentityParams { get; } = new IccTrcParameters();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IccTrcParameters[] FillParams(IccTrcParameters[] parameters)
        {
            var arr = new IccTrcParameters[4];
            for (int i = 0; i < 4; i++)
            {
                arr[i] = (parameters != null && i < parameters.Length && parameters[i] != null)
                    ? parameters[i]
                    : IdentityParams;
            }
            return arr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 FromParameters(IccTrcParameters[] parameters, Func<IccTrcParameters, float> func)
        {
            return new Vector4(func(parameters[0]), func(parameters[1]), func(parameters[2]), func(parameters[3]));
        }
    }
}
