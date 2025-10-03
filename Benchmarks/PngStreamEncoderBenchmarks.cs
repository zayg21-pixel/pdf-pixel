using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using PdfReader.Rendering.Image.Png;
using Microsoft.VSDiagnostics;

namespace Benchmarks
{
    [CPUUsageDiagnoser]
    public class PngStreamEncoderBenchmarks
    {
        private byte[] _rowData;
        private MemoryStream _outputStream;
        private PngStreamEncoder _encoder;
        private const int Width = 1024;
        private const int Height = 1024;
        [GlobalSetup]
        public void Setup()
        {
            _rowData = new byte[Width * 4];
            var random = new Random(42);
            random.NextBytes(_rowData);
            _outputStream = new MemoryStream();
            _encoder = new PngStreamEncoder(_outputStream, Width, Height);
        }

        [Benchmark]
        public void EncodeFullImage()
        {
            _outputStream.SetLength(0);
            _encoder = new PngStreamEncoder(_outputStream, Width, Height);
            for (int row = 0; row < Height; row++)
            {
                _encoder.WriteRow(_rowData);
            }

            _encoder.Finish();
        }
    }
}