using BenchmarkDotNet.Attributes;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Geometry.PathOperations;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class StrokeOutlineBenchmark
    {
        private const int ZigzagSegmentCount = 2000;
        private const int OvalCount = 500;

        private readonly PdfPath _zigzag = CreateZigzag();
        private readonly PdfPath _ovals = CreateOvals();
        private readonly PdfMatrix _deviceMatrix = PdfMatrix.CreateScale(2f, 2f);
        private readonly PdfMatrix _anisotropicPen = PdfMatrix.CreateScale(1f, 0.5f);
        private readonly PdfStrokeStyle _miterStyle = new(lineWidth: 2f);
        private readonly PdfStrokeStyle _roundStyle = new(lineWidth: 4f, lineCap: PdfStrokeCap.Round, lineJoin: PdfStrokeJoin.Round);
        private readonly PdfStrokeStyle _dashedStyle = new(lineWidth: 1.5f, dashPattern: [6f, 3f]);

        [Benchmark]
        public PdfPath ZigzagMiter() => PdfStrokeOutlineBuilder.BuildOutline(_zigzag, _miterStyle, PdfMatrix.Identity, _deviceMatrix);

        [Benchmark]
        public PdfPath ZigzagRound() => PdfStrokeOutlineBuilder.BuildOutline(_zigzag, _roundStyle, PdfMatrix.Identity, _deviceMatrix);

        [Benchmark]
        public PdfPath ZigzagDashed() => PdfStrokeOutlineBuilder.BuildOutline(_zigzag, _dashedStyle, PdfMatrix.Identity, _deviceMatrix);

        [Benchmark]
        public PdfPath OvalsMiter() => PdfStrokeOutlineBuilder.BuildOutline(_ovals, _miterStyle, PdfMatrix.Identity, _deviceMatrix);

        [Benchmark]
        public PdfPath OvalsDashed() => PdfStrokeOutlineBuilder.BuildOutline(_ovals, _dashedStyle, PdfMatrix.Identity, _deviceMatrix);

        [Benchmark]
        public PdfPath OvalsAnisotropicPen() => PdfStrokeOutlineBuilder.BuildOutline(_ovals, _miterStyle, _anisotropicPen, _deviceMatrix);

        private static PdfPath CreateZigzag()
        {
            PdfPathBuilder builder = new();
            builder.MoveTo(0f, 0f);

            for (int segment = 1; segment <= ZigzagSegmentCount; segment++)
            {
                float y = (segment % 2 == 0) ? 0f : 20f;
                builder.LineTo(segment * 7f, y);
            }

            return builder.ToPath();
        }

        private static PdfPath CreateOvals()
        {
            PdfPathBuilder builder = new();

            for (int oval = 0; oval < OvalCount; oval++)
            {
                float left = (oval % 25) * 40f;
                float top = (oval / 25) * 30f;
                builder.AddOval(new PdfRectangle(left, top, left + 35f, top + 25f));
            }

            return builder.ToPath();
        }
    }
}
