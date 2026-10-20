using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Model;

/// <summary>
/// Represents a PDF polyline annotation.
/// </summary>
/// <remarks>
/// PolyLine annotations display an open path with multiple vertices on the page.
/// Unlike Polygon, the path is not closed. PolyLine annotations can have line ending
/// styles at the start and end points.
/// </remarks>
public class PdfPolyLineAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolyLineAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this polyline annotation.</param>
    public PdfPolyLineAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.PolyLine)
    {
        Vertices = PdfAnnotationGeometry.ParsePoints(annotationObject.Dictionary.GetArray(PdfTokens.VerticesKey));
        Path = PdfAnnotationGeometry.ParsePath(annotationObject.Dictionary.GetArray(PdfTokens.PathKey));

        if (Path != null)
        {
            DrawingPath = Path;
        }
        else
        {
            DrawingPath = PdfAnnotationGeometry.CreatePolylinePath(Vertices);
        }

        PdfArray? lineEndingArray = annotationObject.Dictionary.GetArray(PdfTokens.LineEndingKey);
        if (lineEndingArray?.Count >= 2)
        {
            StartLineEnding = lineEndingArray.GetNameOrDefault(0).AsEnum<PdfLineEndingStyle>();
            EndLineEnding = lineEndingArray.GetNameOrDefault(1).AsEnum<PdfLineEndingStyle>();
        }

        BorderEffect = PdfAnnotationBorderParser.ParseBorderEffect(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
        StrokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();
        // TODO: [MEDIUM] Not parsed, so the polyline is drawn as a plain line without its measurement:
        // - measurement dictionary for dimension polylines, units and scale (Measure)
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first point of the polyline.
    /// </summary>
    protected override PdfPoint ContentStart => PdfAnnotationGeometry.GetStartPoint(DrawingPath) ?? base.ContentStart;

    /// <inheritdoc/>
    /// <remarks>
    /// When there is no appearance stream, this is the bounding box of the polyline.
    /// Otherwise this is the declared /Rect.
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
    /// Gets the vertices array containing coordinates of the polyline vertices.
    /// </summary>
    public PdfPoint[] Vertices { get; }

    /// <summary>
    /// Gets the polyline built from path operators, PDF 2.0, or null when absent or malformed.
    /// </summary>
    public PdfPath? Path { get; }

    /// <summary>
    /// Gets the line ending style at the start point.
    /// </summary>
    public PdfLineEndingStyle StartLineEnding { get; }

    /// <summary>
    /// Gets the line ending style at the end point.
    /// </summary>
    public PdfLineEndingStyle EndLineEnding { get; }

    /// <summary>
    /// Gets the stroke style used to draw the polyline, falling back to defaults when no BS/Border entry is present.
    /// </summary>
    public PdfStrokeStyle StrokeStyle { get; }

    /// <summary>
    /// Gets the parsed border effect (BE entry).
    /// </summary>
    public PdfAnnotationBorderEffect BorderEffect { get; }

    /// <summary>
    /// Gets the polyline that is drawn: <see cref="Path"/> when present, otherwise <see cref="Vertices"/>.
    /// Null when neither defines a polyline.
    /// </summary>
    internal PdfPath? DrawingPath { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (DrawingPath == null
            || !PdfAnnotationGeometry.TryGetPathEnds(DrawingPath, out PdfPoint start, out PdfPoint afterStart, out PdfPoint end, out PdfPoint beforeEnd))
        {
            return false;
        }

        PdfColor lineColor = ResolveColor(page, PdfColors.Black);

        PdfPaint linePaint = PdfAnnotationPaintFactory.CreateStrokePaint(lineColor, StrokeStyle);

        processor.Process(new DrawPathCommand(DrawingPath, linePaint));

        PdfColor interiorColor = ResolveInteriorColor(page);

        if (StartLineEnding != PdfLineEndingStyle.None)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                start.X,
                start.Y,
                afterStart.X,
                afterStart.Y,
                StartLineEnding,
                StrokeStyle.LineWidth,
                lineColor,
                interiorColor);
        }

        if (EndLineEnding != PdfLineEndingStyle.None)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                end.X,
                end.Y,
                beforeEnd.X,
                beforeEnd.Y,
                EndLineEnding,
                StrokeStyle.LineWidth,
                lineColor,
                interiorColor);
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this polyline annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"PolyLine Annotation: {Contents}";
        }

        return "PolyLine Annotation";
    }
}
