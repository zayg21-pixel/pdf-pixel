using System.Numerics;
using BenchmarkDotNet.Attributes;
using PdfReader.Color.Icc.Utilities;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class SampledTrcVectorBenchmarks
    {
        private float[][] _samples;
        private SampledTrcVectorEvaluator _vectorEvaluator;
        private IIccTrcEvaluator[] _scalarEvaluators;
        private Vector4[] _inputs;
        private const int InputCount = 1000;
        private const int LutSize = 256;

        [GlobalSetup]
        public void Setup()
        {
            _samples = new float[4][];
            var rand = new Random(123);
            for (int c = 0; c < 4; c++)
            {
                _samples[c] = new float[LutSize];
                for (int i = 0; i < LutSize; i++)
                {
                    // Example: monotonic LUT with some nonlinearity
                    _samples[c][i] = (float)Math.Pow(i / (float)(LutSize - 1), 1.5 + 0.2 * c);
                }
            }
            _vectorEvaluator = new SampledTrcVectorEvaluator(_samples);
            _scalarEvaluators = new IIccTrcEvaluator[4];
            for (int c = 0; c < 4; c++)
            {
                var trc = PdfReader.Color.Icc.Model.IccTrc.FromSamples(_samples[c]);
                _scalarEvaluators[c] = IccTrcEvaluatorFactory.Create(trc);
            }
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
