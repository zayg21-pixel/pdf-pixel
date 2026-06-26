using PdfPixel.Annotations.Rendering;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
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
            Vertices = new SKPoint[vertices.Length / 2];

            for (int i = 0; i < vertices.Length; i += 2)
            {
                Vertices[i / 2] = new SKPoint(vertices[i], vertices[i + 1]);
            }
        }
        else
        {
            Vertices = Array.Empty<SKPoint>();
        }

        PdfArray? lineEndingArray = annotationObject.Dictionary.GetArray(PdfTokens.LineEndingKey);
        if (lineEndingArray?.Count >= 2)
        {
            StartLineEnding = lineEndingArray.GetName(0).AsEnum<PdfLineEndingStyle>();
            EndLineEnding = lineEndingArray.GetName(1).AsEnum<PdfLineEndingStyle>();
        }

        BorderEffect = PdfBorderEffect.FromDictionary(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
        // TODO: [MEDIUM] IT (Intent: PolyLineDimension), Measure
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first vertex of the polyline.
    /// </summary>
    protected override SKPoint ContentStart => (Vertices?.Length > 0) ? Vertices[0] : base.ContentStart;

    /// <summary>
    /// Gets the vertices array containing coordinates of the polyline vertices.
    /// </summary>
    public SKPoint[] Vertices { get; }

    /// <summary>
    /// Gets the line ending style at the start point.
    /// </summary>
    public PdfLineEndingStyle StartLineEnding { get; }

    /// <summary>
    /// Gets the line ending style at the end point.
    /// </summary>
    public PdfLineEndingStyle EndLineEnding { get; }

    /// <summary>
    /// Gets the border effect applied to this annotation's border, or null for no effect.
    /// </summary>
    public PdfBorderEffect? BorderEffect { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (Vertices.Length < 2)
        {
            return false;
        }

        SKColor lineColor = ResolveColor(page, SKColors.Black);
        float lineWidth = BorderStyle?.Width ?? 1.0f;

        using SKPathBuilder pathBuilder = new();

        pathBuilder.MoveTo(Vertices[0]);

        for (int i = 1; i < Vertices.Length; i++)
        {
            pathBuilder.LineTo(Vertices[i]);
        }

        SKPaint linePaint = new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            StrokeJoin = SKStrokeJoin.Miter,
            StrokeCap = SKStrokeCap.Butt,
            Color = lineColor
        };

        BorderStyle?.TryApplyEffect(linePaint, lineColor);
        BorderEffect?.TryApplyEffect(linePaint, lineWidth);

        processor.Process(new DrawPathCommand(pathBuilder.Detach(), linePaint));

        SKColor interiorSKColor = ResolveInteriorColor(page);

        if (StartLineEnding != PdfLineEndingStyle.None && Vertices.Length >= 2)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                Vertices[0].X,
                Vertices[0].Y,
                Vertices[1].X,
                Vertices[1].Y,
                StartLineEnding,
                lineWidth,
                lineColor,
                interiorSKColor);
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
                lineWidth,
                lineColor,
                interiorSKColor);
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this polyline annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"PolyLine Annotation: {contentsText}";
        }

        return "PolyLine Annotation";
    }
}
