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

        int startPoint = 0;
        foreach (int endPoint in outline.EndPoints.Span)
        {
            EmitContour(pathBuilder, outline, startPoint, endPoint);
            startPoint = endPoint + 1;
        }

        return pathBuilder.Detach();
    }

    /// <summary>
    /// Emits one contour's on/off-curve points as path segments, synthesizing the implied on-curve
    /// midpoint between two consecutive off-curve points, which TrueType allows omitting.
    /// </summary>
    private static void EmitContour(PdfFontPathBuilder pathBuilder, GlyphOutline outline, int startPoint, int endPoint)
    {
        int pointCount = endPoint - startPoint + 1;
        if (pointCount <= 0)
        {
            return;
        }

        int firstOnCurve = 0;
        while (firstOnCurve < pointCount && !IsContourPointOnCurve(outline, startPoint, pointCount, firstOnCurve))
        {
            firstOnCurve++;
        }

        PdfFontPoint startPointCoordinates;
        if (firstOnCurve == pointCount)
        {
            // All-off-curve contour: start at the implied midpoint of the first two points.
            PdfFontPoint first = GetContourPoint(outline, startPoint, pointCount, 0);
            PdfFontPoint second = GetContourPoint(outline, startPoint, pointCount, 1);
            startPointCoordinates = Midpoint(first, second);
            firstOnCurve = 0;
        }
        else
        {
            startPointCoordinates = GetContourPoint(outline, startPoint, pointCount, firstOnCurve);
        }

        pathBuilder.MoveTo(startPointCoordinates);

        PdfFontPoint currentPoint = startPointCoordinates;
        PdfFontPoint? pendingControlPoint = null;

        for (int step = 1; step <= pointCount; step++)
        {
            int index = firstOnCurve + step;
            bool onCurve = IsContourPointOnCurve(outline, startPoint, pointCount, index);
            PdfFontPoint point = GetContourPoint(outline, startPoint, pointCount, index);

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

    private static bool IsContourPointOnCurve(GlyphOutline outline, int startPoint, int pointCount, int index)
        => (outline.Flags.Span[startPoint + (index % pointCount)] & SfntGlyphFlags.OnCurve) != 0;

    private static PdfFontPoint GetContourPoint(GlyphOutline outline, int startPoint, int pointCount, int index)
        => outline.Points.Span[startPoint + (index % pointCount)];
}
