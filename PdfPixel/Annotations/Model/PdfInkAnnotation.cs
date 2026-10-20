using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System.Linq;

namespace PdfPixel.Annotations.Model;

/// <summary>
/// Represents a PDF ink annotation for freehand drawing.
/// </summary>
/// <remarks>
/// Ink annotations represent freehand "scribbles" composed of one or more disjoint paths.
/// When displayed or printed, the paths are stroked with the annotation's color
/// using a solid line of uniform thickness.
/// </remarks>
public class PdfInkAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfInkAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this ink annotation.</param>
    public PdfInkAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Ink)
    {
        InkList = PdfAnnotationGeometryParser.ParseInkList(annotationObject.Dictionary.GetArray(PdfTokens.InkListKey));
        Path = PdfAnnotationGeometryParser.ParsePath(annotationObject.Dictionary.GetArray(PdfTokens.PathKey));

        StrokeStyle = (BorderStyle?.StrokeStyle ?? new PdfStrokeStyle()).WithLineCap(PdfStrokeCap.Round).WithLineJoin(PdfStrokeJoin.Round);
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first point of <see cref="Path"/>, or of the
    /// first ink path when there is no <see cref="Path"/>.
    /// </summary>
    protected override PdfPoint ContentStart
    {
        get
        {
            if (Path != null)
            {
                PdfPathSegmentEnumerator segments = Path.Segments.GetEnumerator();

                if (segments.MoveNext())
                {
                    return segments.Current.Points[0];
                }
            }

            return (InkList?.Length > 0 && InkList[0].Length > 0) ? InkList[0][0] : base.ContentStart;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A declared /Rect that misses <see cref="Path"/>, or <see cref="InkList"/> when there is no
    /// <see cref="Path"/>, entirely cannot be where the ink is, so the ink bounds are used instead, grown to
    /// leave room for the stroke around them.
    /// </remarks>
    public override PdfRectangle Rectangle
    {
        get
        {
            if (Appearance != null)
            {
                return base.Rectangle;
            }

            PdfRectangle? inkBounds = (Path != null)
                ? Path.GetBounds()
                : PdfRectangle.FromPoints(InkList.SelectMany(static path => path));

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
    /// Gets the ink stroke built from path operators, PDF 2.0. Drawn instead of <see cref="InkList"/> when
    /// present; null when absent or malformed.
    /// </summary>
    public PdfPath? Path { get; }

    /// <summary>
    /// Gets the stroke style used to draw the ink paths (always round cap/join), falling back to
    /// defaults when no BS/Border entry is present.
    /// </summary>
    public PdfStrokeStyle StrokeStyle { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        PdfColor inkColor = ResolveColor(page, PdfColors.Black);

        if (Path != null)
        {
            PdfPaint pathPaint = PdfAnnotationPaintFactory.CreateStrokePaint(inkColor, StrokeStyle);
            processor.Process(new DrawPathCommand(Path, pathPaint));

            return true;
        }

        if (InkList == null || InkList.Length == 0)
        {
            return false;
        }

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

        if (Contents != null)
        {
            return $"Ink Annotation ({pathCount} paths): {Contents}";
        }

        return $"Ink Annotation ({pathCount} paths)";
    }
}
