using System.Numerics;
using PdfReader.Functions;
using PdfReader.Color.Icc.Model;
using System;
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
            _breakpoint = new Vector4(parameters[0].Breakpoint, parameters[1].Breakpoint, parameters[2].Breakpoint, parameters[3].Breakpoint);
            _constantC = new Vector4(parameters[0].ConstantC, parameters[1].ConstantC, parameters[2].ConstantC, parameters[3].ConstantC);
            _scale = new Vector4(parameters[0].Scale, parameters[1].Scale, parameters[2].Scale, parameters[3].Scale);
            _offset = new Vector4(parameters[0].Offset, parameters[1].Offset, parameters[2].Offset, parameters[3].Offset);
            var gammaVector = new Vector4(parameters[0].Gamma, parameters[1].Gamma, parameters[2].Gamma, parameters[3].Gamma);
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
        private readonly float[][] _samples;
        private readonly Vector4 _scale;

        public SampledTrcVectorEvaluator(float[][] samples)
        {
            if (samples == null || samples.Length == 0 || samples.Length > 4)
            {
                throw new ArgumentException("samples must be an array of 1 to 4 float[]", nameof(samples));
            }
            _samples = new float[4][];
            for (int i = 0; i < 4; i++)
            {
                if (i < samples.Length && samples[i] != null && samples[i].Length > 0)
                {
                    _samples[i] = samples[i];
                }
                else
                {
                    _samples[i] = new float[] { 0f, 1f };
                }
            }
            _scale = new Vector4(
                _samples[0].Length - 1,
                _samples[1].Length - 1,
                _samples[2].Length - 1,
                _samples[3].Length - 1
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            Vector4 scaled = x * _scale;
            Vector4 clamped = Vector4.Clamp(scaled, Vector4.Zero, _scale);

            int idx0x = (int)clamped.X;
            int idx0y = (int)clamped.Y;
            int idx0z = (int)clamped.Z;
            int idx0w = (int)clamped.W;

            int idx1x = Math.Min(idx0x + 1, _samples[0].Length - 1);
            int idx1y = Math.Min(idx0y + 1, _samples[1].Length - 1);
            int idx1z = Math.Min(idx0z + 1, _samples[2].Length - 1);
            int idx1w = Math.Min(idx0w + 1, _samples[3].Length - 1);

            Vector4 idx0 = new Vector4(idx0x, idx0y, idx0z, idx0w);
            Vector4 frac = clamped - idx0;

            float v0x = _samples[0][idx0x];
            float v0y = _samples[1][idx0y];
            float v0z = _samples[2][idx0z];
            float v0w = _samples[3][idx0w];

            float v1x = _samples[0][idx1x];
            float v1y = _samples[1][idx1y];
            float v1z = _samples[2][idx1z];
            float v1w = _samples[3][idx1w];

            Vector4 v0 = new Vector4(v0x, v0y, v0z, v0w);
            Vector4 v1 = new Vector4(v1x, v1y, v1z, v1w);
            Vector4 result = v0 + (v1 - v0) * frac;

            return result;
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
            _breakpoint = new Vector4(parameters[0].Breakpoint, parameters[1].Breakpoint, parameters[2].Breakpoint, parameters[3].Breakpoint);
            _constantC = new Vector4(parameters[0].ConstantC, parameters[1].ConstantC, parameters[2].ConstantC, parameters[3].ConstantC);
            _scale = new Vector4(parameters[0].Scale, parameters[1].Scale, parameters[2].Scale, parameters[3].Scale);
            _offset = new Vector4(parameters[0].Offset, parameters[1].Offset, parameters[2].Offset, parameters[3].Offset);
            _powerOffset = new Vector4(parameters[0].PowerOffset, parameters[1].PowerOffset, parameters[2].PowerOffset, parameters[3].PowerOffset);
            _linearOffset = new Vector4(parameters[0].LinearOffset, parameters[1].LinearOffset, parameters[2].LinearOffset, parameters[3].LinearOffset);
            var gammaVector = new Vector4(parameters[0].Gamma, parameters[1].Gamma, parameters[2].Gamma, parameters[3].Gamma);
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
            _breakpoint = new Vector4(parameters[0].Breakpoint, parameters[1].Breakpoint, parameters[2].Breakpoint, parameters[3].Breakpoint);
            _constantC = new Vector4(parameters[0].ConstantC, parameters[1].ConstantC, parameters[2].ConstantC, parameters[3].ConstantC);
            _scale = new Vector4(parameters[0].Scale, parameters[1].Scale, parameters[2].Scale, parameters[3].Scale);
            _offset = new Vector4(parameters[0].Offset, parameters[1].Offset, parameters[2].Offset, parameters[3].Offset);
            var gammaVector = new Vector4(parameters[0].Gamma, parameters[1].Gamma, parameters[2].Gamma, parameters[3].Gamma);
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
            _breakpoint = new Vector4(parameters[0].Breakpoint, parameters[1].Breakpoint, parameters[2].Breakpoint, parameters[3].Breakpoint);
            _scale = new Vector4(parameters[0].Scale, parameters[1].Scale, parameters[2].Scale, parameters[3].Scale);
            _offset = new Vector4(parameters[0].Offset, parameters[1].Offset, parameters[2].Offset, parameters[3].Offset);
            var gammaVector = new Vector4(parameters[0].Gamma, parameters[1].Gamma, parameters[2].Gamma, parameters[3].Gamma);
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
    /// Optimized evaluator for single-channel ICC TRC using the scalar IIccTrcEvaluator directly.
    /// </summary>
    internal sealed class SingleChannelTrcVectorEvaluator : IIccTrcVectorEvaluator
    {
        private readonly IIccTrcEvaluator _evaluator;

        public SingleChannelTrcVectorEvaluator(IccTrc trc)
        {
            if (trc == null)
            {
                throw new ArgumentNullException(nameof(trc));
            }
            _evaluator = trc.Evaluator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Evaluate(Vector4 x)
        {
            float result = _evaluator.Evaluate(x.X);
            return new Vector4(result, 1.0f, 1.0f, 1.0f);
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
    }
}
