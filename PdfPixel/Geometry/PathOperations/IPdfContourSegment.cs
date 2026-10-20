using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// One segment of a <see cref="PdfContourMeasure"/>.
/// </summary>
internal interface IPdfContourSegment
{
    Vector2 Start { get; }

    Vector2 End { get; }

    bool IsDegenerate { get; }

    Vector2 StartTangent();

    Vector2 EndTangent();

    /// <summary>
    /// The point at parameter <paramref name="t"/>.
    /// </summary>
    Vector2 Evaluate(float t);

    /// <summary>
    /// Appends this segment to <paramref name="builder"/>, whose current point is <see cref="Start"/>.
    /// </summary>
    void AppendTo(PdfPathBuilder builder);

    /// <summary>
    /// Builds an arc-length lookup backed by <paramref name="buffer"/> rather than by an allocation of
    /// its own. The caller must finish with one segment's lookup before asking for the next, since the
    /// next one overwrites the buffer. Ignored by segment kinds that don't sample a curve.
    /// </summary>
    PdfLengthLookup CreateLengthLookup(PdfCurveLengthBuffer buffer);

    IPdfContourSegment Slice(float t0, float t1);

    /// <summary>
    /// This segment with every point mapped by <paramref name="matrix"/>.
    /// </summary>
    IPdfContourSegment Transform(in PdfMatrix matrix);

    /// <summary>
    /// Emits this segment's offset rail, walking it end to start when <paramref name="reversed"/> is
    /// set. <paramref name="startTangent"/> and <paramref name="endTangent"/> are the unit tangents at
    /// the ends of the walk, which the caller already holds because it places the joins between
    /// segments.
    /// </summary>
    void EmitOffset(PdfPathBuilder result, float halfWidth, float offsetTolerance, Vector2 startTangent, Vector2 endTangent, bool reversed);
}
