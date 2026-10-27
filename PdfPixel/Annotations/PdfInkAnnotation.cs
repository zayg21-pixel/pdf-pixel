using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

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
    internal PdfInkAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Ink)
    {
        InkList = PdfAnnotationGeometryParser.ParseInkList(annotationObject.Dictionary.GetArray(PdfTokens.InkListKey));
        Path = PdfAnnotationGeometryParser.ParsePath(annotationObject.Dictionary.GetArray(PdfTokens.PathKey));

        if (Path != null)
        {
            DrawingPath = Path;
        }
        else
        {
            DrawingPath = CreateInkListPath(InkList);
        }

        StrokeStyle = (BorderStyle?.StrokeStyle ?? new PdfStrokeStyle()).WithLineCap(PdfStrokeCap.Round).WithLineJoin(PdfStrokeJoin.Round);
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first point of the ink.
    /// </summary>
    protected override PdfPoint ContentStart => PdfAnnotationGeometry.GetStartPoint(DrawingPath) ?? base.ContentStart;

    /// <inheritdoc/>
    /// <remarks>
    /// A declared /Rect that misses the ink entirely cannot be where the ink is, so the ink bounds are used
    /// instead, grown to leave room for the stroke around them.
    /// </remarks>
    public override PdfRectangle Rectangle
    {
        get
        {
            if (Appearance != null)
            {
                return base.Rectangle;
            }

            return PdfAnnotationGeometry.GetFallbackRectangle(base.Rectangle, DrawingPath, StrokeStyle.LineWidth);
        }
    }

    /// <summary>
    /// Gets the parsed ink list as an array of arrays of PdfPoint.
    /// Each inner array represents a path (sequence of points).
    /// </summary>
    public PdfPoint[][] InkList { get; }

    /// <summary>
    /// Gets the ink stroke built from path operators, PDF 2.0, or null when absent or malformed.
    /// </summary>
    public PdfPath? Path { get; }

    /// <summary>
    /// Gets the stroke style used to draw the ink paths (always round cap/join), falling back to
    /// defaults when no BS/Border entry is present.
    /// </summary>
    public PdfStrokeStyle StrokeStyle { get; }

    /// <summary>
    /// Gets the ink that is drawn: <see cref="Path"/> when present, otherwise the smoothed strokes of
    /// <see cref="InkList"/>. Null when neither defines a stroke.
    /// </summary>
    internal PdfPath? DrawingPath { get; }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        if (DrawingPath == null)
        {
            return false;
        }

        PdfColor inkColor = ResolveColor(page, PdfColors.Black);
        PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(inkColor, StrokeStyle);

        processor.Process(new DrawPathCommand(DrawingPath, paint));

        return true;
    }

    private static PdfPath? CreateInkListPath(PdfPoint[][] inkList)
    {
        PdfPathBuilder path = new();

        foreach (PdfPoint[] points in inkList)
        {
            if (points.Length < 2)
            {
                continue;
            }

            BuildSmoothPath(path, points);
        }

        if (path.IsEmpty)
        {
            return null;
        }

        return path.ToPath();
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
