using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Linq;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF ink annotation for freehand drawing.
/// </summary>
/// <remarks>
/// Ink annotations represent freehand "scribbles" composed of one or more disjoint paths.
/// When displayed or printed, the paths are stroked with the annotation's color
/// using a solid line of uniform thickness.
/// </remarks>
public class PdfInkAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfInkAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this ink annotation.</param>
    public PdfInkAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Ink)
    {
        PdfArray? inkList = annotationObject.Dictionary.GetArray(PdfTokens.InkListKey);
        InkList = ParseInkList(inkList);

        StrokeStyle = (BorderStyle?.StrokeStyle ?? new PdfStrokeStyle()).WithLineCap(PdfStrokeCap.Round).WithLineJoin(PdfStrokeJoin.Round);
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first point of the first ink path.
    /// </summary>
    protected override PdfPoint ContentStart => (InkList?.Length > 0 && InkList[0].Length > 0) ? InkList[0][0] : base.ContentStart;

    /// <inheritdoc/>
    /// <remarks>
    /// A declared /Rect that misses <see cref="InkList"/> entirely cannot be where the ink is, so the ink
    /// bounds are used instead, grown to leave room for the stroke around them.
    /// </remarks>
    public override PdfRectangle Rectangle
    {
        get
        {
            if (AppearanceDictionary != null)
            {
                return base.Rectangle;
            }

            PdfRectangle? inkBounds = PdfRectangle.FromPoints(InkList.SelectMany(static path => path));

            if (inkBounds == null)
            {
                return base.Rectangle;
            }

            PdfRectangle strokeBounds = inkBounds.Value.Inflate(2f * StrokeStyle.LineWidth);

            return (PdfRectangle.IntersectsWith(base.Rectangle, strokeBounds)) ? base.Rectangle : strokeBounds;
        }
    }

    /// <summary>
    /// Gets the parsed ink list as an array of arrays of PdfPoint.
    /// Each inner array represents a path (sequence of points).
    /// </summary>
    public PdfPoint[][] InkList { get; }

    /// <summary>
    /// Gets the stroke style used to draw the ink paths (always round cap/join), falling back to
    /// defaults when no BS/Border entry is present.
    /// </summary>
    public PdfStrokeStyle StrokeStyle { get; }

    private static PdfPoint[][] ParseInkList(PdfArray? inkList)
    {
        if (inkList == null || inkList.Count == 0)
        {
            return Array.Empty<PdfPoint[]>();
        }

        var result = new PdfPoint[inkList.Count][];
        for (int i = 0; i < inkList.Count; i++)
        {
            PdfArray? pathArray = inkList.GetArray(i);
            if (pathArray == null || pathArray.Count < 4)
            {
                result[i] = Array.Empty<PdfPoint>();
                continue;
            }

            float[] coords = pathArray.GetFloatArray();
            if (coords == null || coords.Length < 4)
            {
                result[i] = Array.Empty<PdfPoint>();
                continue;
            }

            var points = new PdfPoint[coords.Length / 2];
            int p = 0;
            for (int j = 0; j < coords.Length - 1; j += 2)
            {
                points[p++] = new PdfPoint(coords[j], coords[j + 1]);
            }

            result[i] = points;
        }

        return result;
    }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (InkList == null || InkList.Length == 0)
        {
            return false;
        }

        PdfColor inkColor = ResolveColor(page, PdfColors.Black);

        // Render each path in the parsed ink list
        foreach (PdfPoint[] points in InkList)
        {
            if (points == null || points.Length < 2)
            {
                continue;
            }

            PdfPathBuilder path = new();
            BuildSmoothPath(path, points);

            PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(inkColor, StrokeStyle);

            processor.Process(new DrawPathCommand(path.ToPath(), paint));
        }

        return true;
    }

    private static void BuildSmoothPath(PdfPathBuilder path, PdfPoint[] points)
    {
        path.MoveTo(points[0]);

        if (points.Length == 2)
        {
            path.LineTo(points[1]);
            return;
        }

        for (int i = 0; i < points.Length - 1; i++)
        {
            PdfPoint prevPoint = (i > 0) ? points[i - 1] : points[0];
            PdfPoint currentPoint = points[i];
            PdfPoint nextPoint = points[i + 1];
            PdfPoint farPoint = (i < points.Length - 2) ? points[i + 2] : points[points.Length - 1];

            PdfPoint control1 = new(
                currentPoint.X + (nextPoint.X - prevPoint.X) / 6f,
                currentPoint.Y + (nextPoint.Y - prevPoint.Y) / 6f);
            PdfPoint control2 = new(
                nextPoint.X - (farPoint.X - currentPoint.X) / 6f,
                nextPoint.Y - (farPoint.Y - currentPoint.Y) / 6f);

            path.CubicTo(control1, control2, nextPoint);
        }
    }

    /// <summary>
    /// Returns a string representation of this ink annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and path count.</returns>
    public override string ToString()
    {
        int pathCount = InkList?.Length ?? 0;

        if (Contents?.IsEmpty == false)
        {
            return $"Ink Annotation ({pathCount} paths): {Contents}";
        }

        return $"Ink Annotation ({pathCount} paths)";
    }
}
