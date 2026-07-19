using PdfPixel.Geometry;
using System;

namespace PdfPixel.Geometry;

/// <summary>
/// A single segment of a <see cref="PdfPath"/>.
/// </summary>
public sealed class PdfPathSegment
{
    /// <summary>
    /// Initializes a new <see cref="PdfPathSegment"/> from its type and points.
    /// </summary>
    public PdfPathSegment(PdfPathSegmentType type, PdfPoint[] points)
    {
        Type = type;
        Points = points;
    }

    /// <summary>
    /// The kind of this segment.
    /// </summary>
    public PdfPathSegmentType Type { get; }

    /// <summary>
    /// The points carried by this segment: one point for <see cref="PdfPathSegmentType.MoveTo"/>
    /// and <see cref="PdfPathSegmentType.LineTo"/>, three for <see cref="PdfPathSegmentType.CubicTo"/>
    /// (control point 1, control point 2, end point), none for <see cref="PdfPathSegmentType.Close"/>.
    /// </summary>
    public PdfPoint[] Points { get; }

    /// <summary>
    /// Creates a <see cref="PdfPathSegmentType.MoveTo"/> segment.
    /// </summary>
    public static PdfPathSegment MoveTo(in PdfPoint point) => new(PdfPathSegmentType.MoveTo, [point]);

    /// <summary>
    /// Creates a <see cref="PdfPathSegmentType.LineTo"/> segment.
    /// </summary>
    public static PdfPathSegment LineTo(in PdfPoint point) => new(PdfPathSegmentType.LineTo, [point]);

    /// <summary>
    /// Creates a <see cref="PdfPathSegmentType.CubicTo"/> segment.
    /// </summary>
    public static PdfPathSegment CubicTo(in PdfPoint control1, in PdfPoint control2, in PdfPoint end)
        => new(PdfPathSegmentType.CubicTo, [control1, control2, end]);

    /// <summary>
    /// Creates a <see cref="PdfPathSegmentType.Close"/> segment.
    /// </summary>
    public static PdfPathSegment Close() => new(PdfPathSegmentType.Close, Array.Empty<PdfPoint>());
}
