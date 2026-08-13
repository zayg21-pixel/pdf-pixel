using BenchmarkDotNet.Attributes;
using PdfPixel.Color.Paint;
using PdfPixel.Skia;
using PdfPixel.Geometry;
using SkiaSharp;

namespace Benchmarks;

/// <summary>
/// <see cref="StrokeOutlineBenchmark.CurvesWidePen"/> against the Skia call it stands in for, on the
/// exact same input: the same <see cref="PdfPath"/> converted once to the same <see cref="SKPath"/>, and
/// the same pen converted once to the same <see cref="SKPaint"/>.
/// Run from repo root: dotnet run -c Release --project PdfPixel.Benchmarks -- --filter *CurvesWidePen*
/// </summary>
//[ShortRunJob]
[MemoryDiagnoser]
public class CurvesWidePenBenchmark
{
    private const int SegmentCount = 200;

    private static readonly PdfStrokeStyle WidePen = new(lineWidth: 20f, lineCap: PdfStrokeCap.Butt, lineJoin: PdfStrokeJoin.Miter);

    private PdfPath _curves;
    private SKPath _curvesSk;
    private SKPaint _widePenSk;
    private SKPath _lastSkiaFillPath;

    [GlobalSetup]
    public void Setup()
    {
        _curves = BuildCurves();
        _curvesSk = _curves.ToSkPath();

        _widePenSk = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = WidePen.LineWidth,
            StrokeCap = WidePen.LineCap.ToSkiaStrokeCap(),
            StrokeJoin = WidePen.LineJoin.ToSkiaStrokeJoin(),
            StrokeMiter = WidePen.MiterLimit,
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _lastSkiaFillPath?.Dispose();
        _curvesSk.Dispose();
        _widePenSk.Dispose();
    }

    [Benchmark(Baseline = true)]
    public SKPath Skia()
    {
        _lastSkiaFillPath?.Dispose();

        SKPath fillPath = new();
        _widePenSk.GetFillPath(_curvesSk, fillPath);
        _lastSkiaFillPath = fillPath;

        return fillPath;
    }

    [Benchmark]
    public PdfPath PdfPixel() => PdfStrokeOutlineBuilder.BuildOutline(_curves, WidePen, PdfMatrix.Identity, PdfMatrix.Identity);

    // Identical to StrokeOutlineBenchmark.BuildCurves: an open wave of cubics, each long enough that
    // offsetting it has to split.
    private static PdfPath BuildCurves()
    {
        PdfPathBuilder builder = new();
        builder.MoveTo(0f, 0f);

        for (int index = 0; index < SegmentCount; index++)
        {
            float start = index * 8f;
            float direction = ((index % 2) == 0) ? 12f : -12f;
            builder.CubicTo(start + 2f, direction, start + 6f, direction, start + 8f, 0f);
        }

        return builder.ToPath();
    }
}
