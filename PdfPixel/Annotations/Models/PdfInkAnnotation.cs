using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Geometry;
using PdfPixel.Text;
using SkiaSharp;
using System;

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
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first point of the first ink path.
    /// </summary>
    protected override PdfPoint ContentStart => (InkList?.Length > 0 && InkList[0].Length > 0) ? InkList[0][0] : base.ContentStart;

    /// <summary>
    /// Gets the parsed ink list as an array of arrays of PdfPoint.
    /// Each inner array represents a path (sequence of points).
    /// </summary>
    public PdfPoint[][] InkList { get; }

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

        float lineWidth = BorderStyle?.Width ?? 1.0f;
        SKColor inkColor = ResolveColor(page, SKColors.Black);

        // Render each path in the parsed ink list
        foreach (PdfPoint[] points in InkList)
        {
            if (points == null || points.Length < 2)
            {
                continue;
            }

            PdfPath path = new();
            BuildSmoothPath(path, points);

            SKPaint paint = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = lineWidth,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                Color = inkColor
            };

            BorderStyle?.TryApplyEffect(paint, inkColor);

            processor.Process(new DrawPathCommand(path, paint));
        }

        return true;
    }

    private static void BuildSmoothPath(PdfPath path, PdfPoint[] points)
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
        string contentsText = Contents.ToString();
        int pathCount = InkList?.Length ?? 0;

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Ink Annotation ({pathCount} paths): {contentsText}";
        }

        return $"Ink Annotation ({pathCount} paths)";
    }
}
