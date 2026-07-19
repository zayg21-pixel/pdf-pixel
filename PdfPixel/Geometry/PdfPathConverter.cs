using SkiaSharp;

namespace PdfPixel.Geometry;

/// <summary>
/// Converts <see cref="PdfPath"/> instances to <see cref="SKPath"/> for rendering.
/// </summary>
internal static class PdfPathConverter
{
    /// <summary>
    /// Builds an <see cref="SKPath"/> equivalent to <paramref name="path"/>.
    /// </summary>
    internal static SKPath ToSkPath(this PdfPath path)
    {
        using SKPathBuilder builder = new() { FillType = (path.FillType == PdfPathFillType.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding };

        foreach (PdfPathSegment segment in path.Segments)
        {
            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    builder.MoveTo(segment.Points[0].ToSkPoint());
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    builder.LineTo(segment.Points[0].ToSkPoint());
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    builder.CubicTo(segment.Points[0].ToSkPoint(), segment.Points[1].ToSkPoint(), segment.Points[2].ToSkPoint());
                    break;
                }
                case PdfPathSegmentType.Close:
                {
                    builder.Close();
                    break;
                }
            }
        }

        return builder.Detach();
    }

    /// <summary>
    /// Builds a <see cref="PdfPath"/> equivalent to <paramref name="path"/>, for use only on glyph outline
    /// paths from <c>SKFont.GetGlyphPath</c>. Quadratic segments are elevated to cubic segments, since
    /// <see cref="PdfPath"/> has no quadratic representation. Conic segments are not supported, since
    /// glyph outlines never contain them.
    /// </summary>
    internal static PdfPath ToPdfPath(this SKPath path)
    {
        PdfPath result = new() { FillType = (path.FillType == SKPathFillType.EvenOdd) ? PdfPathFillType.EvenOdd : PdfPathFillType.Winding };

        using SKPath.Iterator iterator = path.CreateIterator(false);
        var points = new SKPoint[4];
        SKPathVerb verb;

        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                {
                    result.MoveTo(points[0].X, points[0].Y);
                    break;
                }
                case SKPathVerb.Line:
                {
                    result.LineTo(points[1].X, points[1].Y);
                    break;
                }
                case SKPathVerb.Quad:
                {
                    AddQuadratic(result, points[0], points[1], points[2]);
                    break;
                }
                case SKPathVerb.Cubic:
                {
                    result.CubicTo(points[1].X, points[1].Y, points[2].X, points[2].Y, points[3].X, points[3].Y);
                    break;
                }
                case SKPathVerb.Close:
                {
                    result.Close();
                    break;
                }
            }
        }

        return result;
    }

    private static void AddQuadratic(PdfPath path, SKPoint start, SKPoint control, SKPoint end)
    {
        const float TwoThirds = 2f / 3f;

        PdfPoint control1 = new(start.X + (TwoThirds * (control.X - start.X)), start.Y + (TwoThirds * (control.Y - start.Y)));
        PdfPoint control2 = new(end.X + (TwoThirds * (control.X - end.X)), end.Y + (TwoThirds * (control.Y - end.Y)));

        path.CubicTo(control1, control2, new PdfPoint(end.X, end.Y));
    }
}
