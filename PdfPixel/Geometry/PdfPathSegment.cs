using System;

namespace PdfPixel.Geometry;

/// <summary>
/// A view over a single segment read from a <see cref="PdfPath"/>'s internal binary buffer.
/// </summary>
public readonly ref struct PdfPathSegment
{
    /// <summary>
    /// Initializes a new <see cref="PdfPathSegment"/> view over its type and points.
    /// </summary>
    // RCS1231 disabled: points is stored into a field and returned to the caller. An "in" parameter's
    // dereferenced value cannot be returned past the current call, so this must stay a by-value parameter.
#pragma warning disable RCS1231
    public PdfPathSegment(PdfPathSegmentType type, ReadOnlySpan<PdfPoint> points)
#pragma warning restore RCS1231
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
    public ReadOnlySpan<PdfPoint> Points { get; }
}
