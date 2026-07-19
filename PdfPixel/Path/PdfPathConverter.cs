using SkiaSharp;

namespace PdfPixel.Path;

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
        using SKPathBuilder builder = new();

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
}
