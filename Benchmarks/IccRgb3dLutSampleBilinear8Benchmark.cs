using System;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using PdfReader.Rendering.Color.Clut;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class IccRgb3dLutSampleBilinear8Benchmark
    {
        private byte[] lut;
        private byte[] rValues;
        private byte[] gValues;
        private byte[] bValues;
        private byte[] output;
        private const int SampleCount = 10000;

        [GlobalSetup]
        public void Setup()
        {
            // Create a dummy LUT with a simple gradient pattern
            int n = TreeDLut.GridSize;
            lut = new byte[n * n * n * 3];
            for (int i = 0; i < lut.Length; i++)
            {
                lut[i] = (byte)(i % 256);
            }

            // Prepare random sample coordinates (as bytes)
            var rand = new Random(42);
            rValues = new byte[SampleCount];
            gValues = new byte[SampleCount];
            bValues = new byte[SampleCount];
            for (int i = 0; i < SampleCount; i++)
            {
                rValues[i] = (byte)rand.Next(0, 256);
                gValues[i] = (byte)rand.Next(0, 256);
                bValues[i] = (byte)rand.Next(0, 256);
            }
            output = new byte[SampleCount * 3];
        }

        //[Benchmark]
        //public void SampleBilinear8_Batch()
        //{
        //    var span = new Span<byte>(output);
        //    for (int i = 0; i < SampleCount; i++)
        //    {
        //        TreeDLut.Sample8Bit(lut, rValues[i], gValues[i], bValues[i], SamlingInterpolation.SampleBilinear, span.Slice(i * 3, 3));
        //    }
        //}

        //[Benchmark]
        //public void SampleBilinear8Simd_Batch()
        //{
        //    var span = new Span<byte>(output);
        //    for (int i = 0; i < SampleCount; i++)
        //    {
        //        IccRgb3dLut.SampleBilinear8Simd(lut, rValues[i], gValues[i], bValues[i], span.Slice(i * 3, 3));
        //    }
        //}

        //[Benchmark]
        //public void SampleBilinear8FixedPoint_Batch()
        //{
        //    var span = new Span<byte>(output);
        //    for (int i = 0; i < SampleCount; i++)
        //    {
        //        TreeDLut.SampleBilinear8FixedPoint(lut, rValues[i], gValues[i], bValues[i], span.Slice(i * 3, 3));
        //    }
        //}
    }
}
