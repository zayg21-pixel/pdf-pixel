using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// A straight segment of a <see cref="PdfContourMeasure"/>.
/// </summary>
internal readonly struct PdfLineContourSegment : IPdfContourSegment
{
    public PdfLineContourSegment(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
    }

    public Vector2 Start { get; }

    public Vector2 End { get; }

    public bool IsDegenerate => Vector2.Distance(Start, End) <= PdfVectorMath.Epsilon;

    public Vector2 StartTangent() => PdfVectorMath.Direction(Start, End);

    public Vector2 EndTangent() => PdfVectorMath.Direction(Start, End);

    /// <inheritdoc />
    public Vector2 Evaluate(float t) => Vector2.Lerp(Start, End, t);

    /// <inheritdoc />
    public void AppendTo(PdfPathBuilder builder) => builder.LineTo(PdfVectorMath.ToPdfPoint(End));

    public PdfLengthLookup CreateLengthLookup(PdfCurveLengthBuffer buffer) => new(Vector2.Distance(Start, End));

    public IPdfContourSegment Slice(float t0, float t1) => new PdfLineContourSegment(Vector2.Lerp(Start, End, t0), Vector2.Lerp(Start, End, t1));

    /// <inheritdoc />
    public IPdfContourSegment Transform(in PdfMatrix matrix) => new PdfLineContourSegment(matrix.MapVector2(Start), matrix.MapVector2(End));

    /// <inheritdoc />
    public void EmitOffset(PdfPathBuilder result, float halfWidth, float offsetTolerance, Vector2 startTangent, Vector2 endTangent, bool reversed)
    {
        Vector2 walkEnd = reversed ? Start : End;
        result.LineTo(PdfVectorMath.ToPdfPoint(PdfVectorMath.Offset(walkEnd, endTangent, halfWidth)));
    }
}
