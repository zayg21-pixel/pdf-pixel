using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF polyline annotation.
/// </summary>
/// <remarks>
/// PolyLine annotations display an open path with multiple vertices on the page.
/// Unlike Polygon, the path is not closed. PolyLine annotations can have line ending
/// styles at the start and end points.
/// </remarks>
public class PdfPolyLineAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolyLineAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this polyline annotation.</param>
    public PdfPolyLineAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.PolyLine)
    {
        float[]? vertices = annotationObject.Dictionary.GetArray(PdfTokens.VerticesKey)?.GetFloatArray();

        if (vertices != null)
        {
            Vertices = new PdfPoint[vertices.Length / 2];

            for (int i = 0; i < vertices.Length; i += 2)
            {
                Vertices[i / 2] = new PdfPoint(vertices[i], vertices[i + 1]);
            }
        }
        else
        {
            Vertices = Array.Empty<PdfPoint>();
        }

        PdfArray? lineEndingArray = annotationObject.Dictionary.GetArray(PdfTokens.LineEndingKey);
        if (lineEndingArray?.Count >= 2)
        {
            StartLineEnding = lineEndingArray.GetNameOrDefault(0).AsEnum<PdfLineEndingStyle>();
            EndLineEnding = lineEndingArray.GetNameOrDefault(1).AsEnum<PdfLineEndingStyle>();
        }

        BorderEffect = PdfAnnotationBorderParser.ParseBorderEffect(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
        StrokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();
        // TODO: [MEDIUM] IT (Intent: PolyLineDimension), Measure
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first vertex of the polyline.
    /// </summary>
    protected override PdfPoint ContentStart => (Vertices?.Length > 0) ? Vertices[0] : base.ContentStart;

    /// <inheritdoc/>
    /// <remarks>
    /// When there is no appearance stream, this is the bounding box of <see cref="Vertices"/>.
    /// Otherwise this is the declared /Rect.
    /// </remarks>
    public override PdfRectangle Rectangle
    {
        get
        {
            if (AppearanceDictionary != null)
            {
                return base.Rectangle;
            }

            PdfRectangle? vertexBounds = PdfRectangle.FromPoints(Vertices);

            if (vertexBounds == null)
            {
                return base.Rectangle;
            }

            PdfRectangle strokeBounds = vertexBounds.Value.Inflate(2f * StrokeStyle.LineWidth);

            return (PdfRectangle.IntersectsWith(base.Rectangle, strokeBounds)) ? base.Rectangle : strokeBounds;
        }
    }

    /// <summary>
    /// Gets the vertices array containing coordinates of the polyline vertices.
    /// </summary>
    public PdfPoint[] Vertices { get; }

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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (Vertices.Length < 2)
        {
            return false;
        }

        PdfColor lineColor = ResolveColor(page, PdfColors.Black);

        PdfPathBuilder path = new();

        path.MoveTo(Vertices[0]);

        for (int i = 1; i < Vertices.Length; i++)
        {
            path.LineTo(Vertices[i]);
        }

        PdfPaint linePaint = PdfAnnotationPaintFactory.CreateStrokePaint(lineColor, StrokeStyle);

        processor.Process(new DrawPathCommand(path.ToPath(), linePaint));

        PdfColor interiorColor = ResolveInteriorColor(page);

        if (StartLineEnding != PdfLineEndingStyle.None && Vertices.Length >= 2)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                Vertices[0].X,
                Vertices[0].Y,
                Vertices[1].X,
                Vertices[1].Y,
                StartLineEnding,
                StrokeStyle.LineWidth,
                lineColor,
                interiorColor);
        }

        if (EndLineEnding != PdfLineEndingStyle.None && Vertices.Length >= 2)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                Vertices[Vertices.Length - 1].X,
                Vertices[Vertices.Length - 1].Y,
                Vertices[Vertices.Length - 2].X,
                Vertices[Vertices.Length - 2].Y,
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
        if (Contents?.IsEmpty == false)
        {
            return $"PolyLine Annotation: {Contents}";
        }

        return "PolyLine Annotation";
    }
}
