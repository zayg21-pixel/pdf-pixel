using BenchmarkDotNet.Attributes;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Skia;
using PdfPixel.Geometry;
using SkiaSharp;

namespace Benchmarks;

/// <summary>
/// <see cref="StrokeOutlineBenchmark.CurvesDashed"/> against the Skia call it stands in for, on the
/// exact same input: the same <see cref="PdfPath"/> converted once to the same <see cref="SKPath"/>, and
/// the same dashed pen converted once to the same <see cref="SKPaint"/>.
/// Run from repo root: dotnet run -c Release --project PdfPixel.Benchmarks -- --filter *CurvesDashed*
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class CurvesDashedBenchmark
{
    private const int SegmentCount = 200;

    private static readonly PdfStrokeStyle Dashed = new(lineWidth: 1f, dashPattern: new[] { 3f, 2f });

    private PdfPath _curves;
    private SKPath _curvesSk;
    private SKPaint _dashedSk;
    private SKPath _lastSkiaFillPath;

    [GlobalSetup]
    public void Setup()
    {
        _curves = BuildCurves();
        _curvesSk = _curves.ToSkPath();

        _dashedSk = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Dashed.LineWidth,
            StrokeCap = Dashed.LineCap.ToSkiaStrokeCap(),
            StrokeJoin = Dashed.LineJoin.ToSkiaStrokeJoin(),
            StrokeMiter = Dashed.MiterLimit,
        };

        _dashedSk.PathEffect = SKPathEffect.CreateDash(Dashed.DashPattern!, Dashed.DashPhase);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _lastSkiaFillPath?.Dispose();
        _curvesSk.Dispose();
        _dashedSk.PathEffect?.Dispose();
        _dashedSk.Dispose();
    }

    [Benchmark(Baseline = true)]
    public SKPath Skia()
    {
        _lastSkiaFillPath?.Dispose();

        SKPath fillPath = new();
        _dashedSk.GetFillPath(_curvesSk, fillPath);
        _lastSkiaFillPath = fillPath;

        return fillPath;
    }

    [Benchmark]
    public PdfPath PdfPixel() => PdfStrokeOutlineBuilder.BuildOutline(_curves, Dashed, PdfMatrix.Identity, PdfMatrix.Identity);

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
