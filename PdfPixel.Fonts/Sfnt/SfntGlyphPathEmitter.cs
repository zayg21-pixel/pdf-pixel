using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Emits a collected glyph outline as a path, converting TrueType's native quadratic curves to the
/// cubic curves <see cref="PdfFontPathBuilder"/> stores.
/// </summary>
public static class SfntGlyphPathEmitter
{
    /// <summary>
    /// Emits every contour of <paramref name="outline"/> as path segments.
    /// </summary>
    /// <param name="outline">The outline to emit.</param>
    /// <param name="matrix">Transform applied to every point of the resulting path.</param>
    public static ReadOnlyMemory<byte> Emit(GlyphOutline outline, in PdfFontMatrix matrix)
    {
        if (outline == null)
        {
            throw new ArgumentNullException(nameof(outline));
        }

        PdfFontPathBuilder pathBuilder = new(matrix);

        ReadOnlySpan<GlyphPoint> points = outline.Points;
        ReadOnlySpan<byte> flags = outline.Flags;

        int startPoint = 0;
        foreach (int endPoint in outline.EndPoints)
        {
            EmitContour(pathBuilder, points, flags, startPoint, endPoint);
            startPoint = endPoint + 1;
        }

        return pathBuilder.Detach();
    }

    /// <summary>
    /// Emits one contour's on/off-curve points as path segments, synthesizing the implied on-curve
    /// midpoint between two consecutive off-curve points, which TrueType allows omitting.
    /// </summary>
    private static void EmitContour(
        PdfFontPathBuilder pathBuilder,
        in ReadOnlySpan<GlyphPoint> points,
        in ReadOnlySpan<byte> flags,
        int startPoint,
        int endPoint)
    {
        int pointCount = endPoint - startPoint + 1;
        if (pointCount <= 0)
        {
            return;
        }

        int firstOnCurve = 0;
        while (firstOnCurve < pointCount && !IsContourPointOnCurve(flags, startPoint, pointCount, firstOnCurve))
        {
            firstOnCurve++;
        }

        PdfFontPoint startPointCoordinates;
        if (firstOnCurve == pointCount)
        {
            // All-off-curve contour: start at the implied midpoint of the first two points.
            PdfFontPoint first = GetContourPoint(points, startPoint, pointCount, 0);
            PdfFontPoint second = GetContourPoint(points, startPoint, pointCount, 1);
            startPointCoordinates = Midpoint(first, second);
            firstOnCurve = 0;
        }
        else
        {
            startPointCoordinates = GetContourPoint(points, startPoint, pointCount, firstOnCurve);
        }

        pathBuilder.MoveTo(startPointCoordinates);

        PdfFontPoint currentPoint = startPointCoordinates;
        PdfFontPoint? pendingControlPoint = null;

        for (int step = 1; step <= pointCount; step++)
        {
            int index = firstOnCurve + step;
            bool onCurve = IsContourPointOnCurve(flags, startPoint, pointCount, index);
            PdfFontPoint point = GetContourPoint(points, startPoint, pointCount, index);

            if (onCurve)
            {
                if (pendingControlPoint.HasValue)
                {
                    pathBuilder.QuadraticTo(currentPoint, pendingControlPoint.Value, point);
                    pendingControlPoint = null;
                }
                else
                {
                    pathBuilder.LineTo(point);
                }

                currentPoint = point;
            }
            else
            {
                if (pendingControlPoint.HasValue)
                {
                    PdfFontPoint previousControl = pendingControlPoint.Value;
                    PdfFontPoint implied = Midpoint(previousControl, point);
                    pathBuilder.QuadraticTo(currentPoint, previousControl, implied);
                    currentPoint = implied;
                }

                pendingControlPoint = point;
            }
        }

        pathBuilder.Close();
    }

    private static PdfFontPoint Midpoint(in PdfFontPoint first, in PdfFontPoint second)
        => new((first.X + second.X) / 2f, (first.Y + second.Y) / 2f);

    private static bool IsContourPointOnCurve(in ReadOnlySpan<byte> flags, int startPoint, int pointCount, int index)
        => (flags[startPoint + (index % pointCount)] & SfntGlyphFlags.OnCurve) != 0;

    private static PdfFontPoint GetContourPoint(in ReadOnlySpan<GlyphPoint> points, int startPoint, int pointCount, int index)
    {
        GlyphPoint point = points[startPoint + (index % pointCount)];

        return new PdfFontPoint(point.X, point.Y);
    }
}
