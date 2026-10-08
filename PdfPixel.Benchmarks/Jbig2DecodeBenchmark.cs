using BenchmarkDotNet.Attributes;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Jbig2.Model;
using System;
using System.IO;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class Jbig2DecodeBenchmark
    {
        private const int PageWidth = 2791;
        private const int PageHeight = 4400;

        private byte[] _pageData = [];

        [GlobalSetup]
        public void Setup() => _pageData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", "oleynikov_page7.jb2e"));

        [Benchmark]
        public Jbig2Bitmap DecodePage()
        {
            Jbig2PageDecoder decoder = new();
            return decoder.Decode(_pageData, PageWidth, PageHeight);
        }
    }
}
