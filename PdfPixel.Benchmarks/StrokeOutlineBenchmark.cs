using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;

namespace Benchmarks;

/// <summary>
/// Measures <see cref="PdfStrokeOutlineBuilder"/> across the stroke features that carry their own
/// geometry: the three caps, the three joins, dashing, curved segments, and the pen width that decides
/// how far an offset curve strays before it is split. Every fixture is 200 segments, so the numbers are
/// directly comparable and a feature that costs several times the baseline shows up as one.
/// Run from repo root: dotnet run -c Release --project PdfPixel.Benchmarks -- --filter *StrokeOutline*
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class StrokeOutlineBenchmark
{
    private const int SegmentCount = 200;
    private const int RectangleCount = SegmentCount / 4;

    private static readonly PdfStrokeStyle ButtMiter = new(lineWidth: 1f, lineCap: PdfStrokeCap.Butt, lineJoin: PdfStrokeJoin.Miter);
    private static readonly PdfStrokeStyle RoundCapRoundJoin = new(lineWidth: 1f, lineCap: PdfStrokeCap.Round, lineJoin: PdfStrokeJoin.Round);
    private static readonly PdfStrokeStyle SquareCapBevelJoin = new(lineWidth: 1f, lineCap: PdfStrokeCap.Square, lineJoin: PdfStrokeJoin.Bevel);
    private static readonly PdfStrokeStyle WidePen = new(lineWidth: 20f, lineCap: PdfStrokeCap.Butt, lineJoin: PdfStrokeJoin.Miter);
    private static readonly PdfStrokeStyle Hairline = new(lineWidth: 0f, lineCap: PdfStrokeCap.Butt, lineJoin: PdfStrokeJoin.Miter);
    private static readonly PdfStrokeStyle Dashed = new(lineWidth: 1f, dashPattern: new[] { 3f, 2f });
    private static readonly PdfStrokeStyle FinelyDashed = new(lineWidth: 1f, dashPattern: new[] { 0.5f, 0.5f });

    // The two factors an anisotropic pen is built with: PdfStrokeOutline shrinks the path by them,
    // strokes it, and grows the outline back, which is the round trip this fixture stands in for.
    private static readonly PdfMatrix PenShrink = PdfMatrix.CreateScale(1f / 3f, 1f);
    private static readonly PdfMatrix PenGrow = PdfMatrix.CreateScale(3f, 1f);

    private readonly PdfPath _lines = BuildLines();
    private readonly PdfPath _sharpZigzag = BuildSharpZigzag();
    private readonly PdfPath _rectangles = BuildRectangles();
    private readonly PdfPath _curves = BuildCurves();

    /// <summary>
    /// The baseline every other case is read against: straight segments, no cap or join geometry of
    /// their own.
    /// </summary>
    [Benchmark(Baseline = true)]
    public PdfPath LinesButtMiter() => PdfStrokeOutlineBuilder.BuildOutline(_lines, ButtMiter);

    /// <summary>
    /// Round caps and joins, which are the ones emitting arcs rather than corners.
    /// </summary>
    [Benchmark]
    public PdfPath LinesRoundCapRoundJoin() => PdfStrokeOutlineBuilder.BuildOutline(_lines, RoundCapRoundJoin);

    [Benchmark]
    public PdfPath LinesSquareCapBevelJoin() => PdfStrokeOutlineBuilder.BuildOutline(_lines, SquareCapBevelJoin);

    /// <summary>
    /// A pen twenty times the baseline's, where the join geometry rather than the path drives the work.
    /// </summary>
    [Benchmark]
    public PdfPath LinesWidePen() => PdfStrokeOutlineBuilder.BuildOutline(_lines, WidePen);

    /// <summary>
    /// A zero line width, which the builder takes to a half-pixel pen of its own.
    /// </summary>
    [Benchmark]
    public PdfPath LinesHairline() => PdfStrokeOutlineBuilder.BuildOutline(_lines, Hairline);

    [Benchmark]
    public PdfPath LinesDashed() => PdfStrokeOutlineBuilder.BuildOutline(_lines, Dashed);

    /// <summary>
    /// A dash pattern short enough to split every segment several times, which is what makes the number
    /// of dashes rather than the number of segments the size of the work.
    /// </summary>
    [Benchmark]
    public PdfPath LinesFinelyDashed() => PdfStrokeOutlineBuilder.BuildOutline(_lines, FinelyDashed);

    /// <summary>
    /// The shrink, stroke and grow an anisotropic pen is built with, against the single pass the same
    /// path takes when the two axes widen alike.
    /// </summary>
    [Benchmark]
    public PdfPath LinesAnisotropicPen()
    {
        PdfPath shrunk = _lines.Transform(PenShrink);
        PdfPath outline = PdfStrokeOutlineBuilder.BuildOutline(shrunk, ButtMiter);

        return outline.Transform(PenGrow);
    }

    /// <summary>
    /// Corners sharp enough to reach the miter limit, where a miter join has to fall back to a bevel.
    /// </summary>
    [Benchmark]
    public PdfPath SharpZigzagMiter() => PdfStrokeOutlineBuilder.BuildOutline(_sharpZigzag, ButtMiter);

    [Benchmark]
    public PdfPath SharpZigzagRoundJoin() => PdfStrokeOutlineBuilder.BuildOutline(_sharpZigzag, RoundCapRoundJoin);

    /// <summary>
    /// Closed subpaths, which join at the seam instead of capping, and the shape most PDFs stroke most.
    /// </summary>
    [Benchmark]
    public PdfPath ClosedRectangles() => PdfStrokeOutlineBuilder.BuildOutline(_rectangles, ButtMiter);

    /// <summary>
    /// Cubic segments, whose offset is split until it stays within tolerance rather than emitted once.
    /// </summary>
    [Benchmark]
    public PdfPath CurvesButtMiter() => PdfStrokeOutlineBuilder.BuildOutline(_curves, ButtMiter);

    [Benchmark]
    public PdfPath CurvesRoundCapRoundJoin() => PdfStrokeOutlineBuilder.BuildOutline(_curves, RoundCapRoundJoin);

    /// <summary>
    /// A wide pen on curves, where the offset strays further from the curve and so splits deeper.
    /// </summary>
    [Benchmark]
    public PdfPath CurvesWidePen() => PdfStrokeOutlineBuilder.BuildOutline(_curves, WidePen);

    /// <summary>
    /// Dashed curves, where every curve is measured by sampling before it can be split at dash
    /// boundaries.
    /// </summary>
    [Benchmark]
    public PdfPath CurvesDashed() => PdfStrokeOutlineBuilder.BuildOutline(_curves, Dashed);

    // An open polyline turning gently at every vertex, as a chart axis or an underline run does.
    private static PdfPath BuildLines()
    {
        PdfPathBuilder builder = new();
        builder.MoveTo(0f, 0f);

        for (int index = 1; index <= SegmentCount; index++)
        {
            builder.LineTo(index * 3f, (index % 2) * 4f);
        }

        return builder.ToPath();
    }

    // The same segment count turning back on itself at every vertex, so that every join is sharp enough
    // to exceed the miter limit.
    private static PdfPath BuildSharpZigzag()
    {
        PdfPathBuilder builder = new();
        builder.MoveTo(0f, 0f);

        for (int index = 1; index <= SegmentCount; index++)
        {
            builder.LineTo(index * 0.4f, (index % 2) * 40f);
        }

        return builder.ToPath();
    }

    // Closed four-edge rectangles, the shape a "re S" leaves.
    private static PdfPath BuildRectangles()
    {
        PdfPathBuilder builder = new();

        for (int index = 0; index < RectangleCount; index++)
        {
            float top = index * 14f;
            builder.MoveTo(0f, top);
            builder.LineTo(120f, top);
            builder.LineTo(120f, top + 10f);
            builder.LineTo(0f, top + 10f);
            builder.Close();
        }

        return builder.ToPath();
    }

    // An open wave of cubics, each long enough that offsetting it has to split.
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
