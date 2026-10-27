using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Measures;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF polyline annotation.
/// </summary>
public class PdfPolyLineAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolyLineAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this polyline annotation.</param>
    internal PdfPolyLineAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.PolyLine)
    {
        Vertices = PdfPoint.ParsePoints(annotationObject.Dictionary.GetArray(PdfTokens.VerticesKey));
        Path = PdfAnnotationGeometryParser.ParsePath(annotationObject.Dictionary.GetArray(PdfTokens.PathKey));

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
        Measure = PdfMeasure.GetMeasure(annotationObject.Dictionary, PdfTokens.MeasureKey);
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first point of the polyline.
    /// </summary>
    protected override PdfPoint ContentStart => PdfAnnotationGeometry.GetStartPoint(DrawingPath) ?? base.ContentStart;

    /// <summary>
    /// Gets the declared rectangle, or the stroked bounds of the polyline when there is no appearance stream
    /// and the declared rectangle does not overlap them.
    /// </summary>
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
    /// Gets the scale and units that apply to the polyline (/Measure), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMeasure? Measure { get; }

    /// <summary>
    /// Gets the polyline that is drawn: <see cref="Path"/> when present, otherwise <see cref="Vertices"/>.
    /// Null when neither defines a polyline.
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
        PdfPathEnds? pathEnds = PdfAnnotationGeometry.GetPathEnds(DrawingPath);

        if (DrawingPath == null || pathEnds == null)
        {
            return false;
        }

        PdfPathEnds ends = pathEnds.Value;

        PdfColor lineColor = ResolveColor(page, PdfColors.Black);

        PdfPaint linePaint = PdfAnnotationPaintFactory.CreateStrokePaint(lineColor, StrokeStyle);

        processor.Process(new DrawPathCommand(DrawingPath, linePaint));

        PdfColor interiorColor = ResolveInteriorColor(page);

        if (StartLineEnding != PdfLineEndingStyle.None)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                ends.Start.X,
                ends.Start.Y,
                ends.AfterStart.X,
                ends.AfterStart.Y,
                StartLineEnding,
                StrokeStyle.LineWidth,
                lineColor,
                interiorColor);
        }

        if (EndLineEnding != PdfLineEndingStyle.None)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                ends.End.X,
                ends.End.Y,
                ends.BeforeEnd.X,
                ends.BeforeEnd.Y,
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
