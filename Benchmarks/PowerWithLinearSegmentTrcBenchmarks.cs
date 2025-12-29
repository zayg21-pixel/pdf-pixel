using System;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Utilities;
using PdfReader.Functions;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class PowerWithLinearSegmentTrcBenchmarks
    {
        private IccTrcParameters[] _parameters;
        private PowerWithLinearSegmentTrcVectorEvaluator _vectorEvaluator;
        private IIccTrcEvaluator[] _scalarEvaluators;
        private Vector4[] _inputs;
        private const int InputCount = 1000;

        [GlobalSetup]
        public void Setup()
        {
            // Example parameters for 4 channels with breakpoint at 0.5 for balanced branching
            _parameters = new[]
            {
                new IccTrcParameters { Gamma = 2.2f, Breakpoint = 0.0f, ConstantC = 12.92f, Scale = 1.055f, Offset = -0.055f },
                new IccTrcParameters { Gamma = 2.0f, Breakpoint = 0.0f, ConstantC = 10.0f, Scale = 1.0f, Offset = 0.0f },
                new IccTrcParameters { Gamma = 2.4f, Breakpoint = 0.0f, ConstantC = 14.0f, Scale = 1.1f, Offset = -0.1f },
                new IccTrcParameters { Gamma = 1.8f, Breakpoint = 0.0f, ConstantC = 8.0f, Scale = 0.9f, Offset = 0.05f },
            };

            _vectorEvaluator = new PowerWithLinearSegmentTrcVectorEvaluator(_parameters);

            _scalarEvaluators = new IIccTrcEvaluator[4];
            for (int i = 0; i < 4; i++)
            {
                var trc = IccTrc.FromParametric(
                    IccTrcParametricType.PowerWithLinearSegment,
                    new float[] {
                        _parameters[i].Gamma,
                        _parameters[i].Scale,
                        _parameters[i].Offset,
                        _parameters[i].ConstantC,
                        _parameters[i].Breakpoint
                    }
                );
                _scalarEvaluators[i] = IccTrcEvaluatorFactory.Create(trc);
            }

            var rand = new Random(42);
            _inputs = new Vector4[InputCount];
            for (int i = 0; i < InputCount; i++)
            {
                _inputs[i] = new Vector4(
                    (float)rand.NextDouble(),
                    (float)rand.NextDouble(),
                    (float)rand.NextDouble(),
                    (float)rand.NextDouble());
            }
        }

        [Benchmark]
        public Vector4 Vectorized()
        {
            Vector4 sum = Vector4.Zero;
            for (int i = 0; i < InputCount; i++)
            {
                sum += _vectorEvaluator.Evaluate(_inputs[i]);
            }
            return sum;
        }

        [Benchmark]
        public Vector4 ScalarSeparate()
        {
            Vector4 sum = Vector4.Zero;
            for (int i = 0; i < InputCount; i++)
            {
                var input = _inputs[i];
                sum += new Vector4(
                    _scalarEvaluators[0].Evaluate(input.X),
                    _scalarEvaluators[1].Evaluate(input.Y),
                    _scalarEvaluators[2].Evaluate(input.Z),
                    _scalarEvaluators[3].Evaluate(input.W));
            }
            return sum;
        }
    }
}
