using BenchmarkDotNet.Attributes;
using PdfPixel.Functions;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Benchmarks
{
    [MemoryDiagnoser]
    //[SimpleJob(RuntimeMoniker.Net80, warmupCount: 1, iterationCount: 10, invocationCount: 1)]
    public class FastPowSeriesBench
    {
        private const float FixedY = 2.75f;
        private const int ArrayLength = 1000;

        private static readonly float[] Inputs = new float[ArrayLength];
        private static readonly FastPowSeriesDegree3 estimator = new FastPowSeriesDegree3(FixedY);
        private static readonly FastPowSeriesDegree3Vector4 vectorEstimator = new FastPowSeriesDegree3Vector4(new Vector4(FixedY));

        static FastPowSeriesBench()
        {
            // Deterministic values in [0, 10], linearly spaced.
            float step = 10.0f / (ArrayLength - 1);
            for (int i = 0; i < ArrayLength; i++)
            {
                Inputs[i] = i * step;
            }
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = ArrayLength)]
        public float MathPow_Array()
        {
            float y = FixedY;
            float acc = 0.0f;
            for (int i = 0; i < Inputs.Length; i++)
            {
                acc += MathF.Pow(Inputs[i], y);
            }

            return acc;
        }

        [Benchmark(OperationsPerInvoke = ArrayLength)]
        public float PowerEstimator_Array()
        {
            float acc = 0.0f;
            for (int i = 0; i < Inputs.Length; i++)
            {
                acc += estimator.Evaluate(Inputs[i]);
            }

            return acc;
        }

        [Benchmark(OperationsPerInvoke = ArrayLength)]
        public float PowerEstimator_Vector4_Array()
        {
            Vector4 accVec = Vector4.Zero;

            // Cast once to a span of Vector4 for the bulk of the work
            ReadOnlySpan<Vector4> vectorSpan = MemoryMarshal.Cast<float, Vector4>(Inputs);
            for (int v = 0; v < vectorSpan.Length; v++)
            {
                Vector4 x = vectorSpan[v];
                Vector4 y = vectorEstimator.Evaluate(x);
                accVec += y;
            }

            // Convert accumulated Vector4 to scalar
            float acc = accVec.X + accVec.Y + accVec.Z + accVec.W;
            return acc;
        }
    }
}
