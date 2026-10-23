using BenchmarkDotNet.Attributes;
using PdfPixel;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using System;
using System.Threading;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class StencilOutlineBenchmark
    {
        private PdfDecodedImage? _samples;
        private PdfDocument? _document;
        private PdfImage? _image;

        [ParamsSource(nameof(MaskNames))]
        public string Mask { get; set; } = StencilMasks.Glyph;

        public static string[] MaskNames => StencilMasks.Names;

        [GlobalSetup]
        public void Setup()
        {
            bool[,] painted = StencilMasks.Create(Mask);
            _samples = StencilMasks.ToDecodedImage(painted);
            _document = StencilMasks.CreateDocument(painted);
            _image = StencilMasks.GetImage(_document);
            StencilOutlineVerification.CheckSame(_samples);
        }

        [GlobalCleanup]
        public void Cleanup() => _document?.Dispose();

        [Benchmark(Baseline = true)]
        public PdfPath? TraceBaseline() => StencilOutlineBaseline.Trace(GetSamples());

        [Benchmark]
        public PdfPath? Trace() => PdfStencilOutline.Trace(GetSamples());

        [Benchmark]
        public PdfDecodedImage Decode()
        {
            PdfCancellationExecutionObserver observer = new(CancellationToken.None);

            return GetImage().DecodePixels(PdfImageDecodingParameters.Default, observer);
        }

        [Benchmark]
        public PdfPath? DecodeAndTrace() => PdfStencilOutline.Trace(GetImage());

        private PdfDecodedImage GetSamples() => _samples ?? throw new InvalidOperationException("Not set up.");

        private PdfImage GetImage() => _image ?? throw new InvalidOperationException("Not set up.");
    }
}
