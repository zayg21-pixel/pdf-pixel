using PdfPixel.Geometry;
using SkiaSharp;

namespace PdfPixel.Skia.Converters;

/// <summary>
/// Converts <see cref="PdfPath"/> instances to <see cref="SKPath"/> for rendering.
/// </summary>
internal static class PdfPathConverter
{
    /// <summary>
    /// Builds an <see cref="SKPath"/> equivalent to <paramref name="path"/>.
    /// </summary>
    internal static SKPath ToSkPath(PdfPath path)
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


    private static void AddQuadratic(PdfPathBuilder path, SKPoint start, SKPoint control, SKPoint end)
    {
        const float TwoThirds = 2f / 3f;

        PdfPoint control1 = new(start.X + (TwoThirds * (control.X - start.X)), start.Y + (TwoThirds * (control.Y - start.Y)));
        PdfPoint control2 = new(end.X + (TwoThirds * (control.X - end.X)), end.Y + (TwoThirds * (control.Y - end.Y)));

        path.CubicTo(control1, control2, new PdfPoint(end.X, end.Y));
    }
}
