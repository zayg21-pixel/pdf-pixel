using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF polygon annotation.
/// </summary>
/// <remarks>
/// Polygon annotations display a closed polygon on the page with multiple vertices.
/// The polygon can be filled with an interior color and stroked with a border.
/// </remarks>
public class PdfPolygonAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolygonAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this polygon annotation.</param>
    public PdfPolygonAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Polygon)
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
            Vertices = System.Array.Empty<SKPoint>();
        }

        BorderEffect = PdfBorderEffect.FromDictionary(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
        // TODO: [MEDIUM] IT (Intent: PolygonCloud, PolygonDimension), Measure
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first vertex of the polygon.
    /// </summary>
    protected override SKPoint ContentStart => (Vertices?.Length > 0) ? Vertices[0] : base.ContentStart;

    /// <summary>
    /// Gets the vertices array containing coordinates of the polygon vertices.
    /// </summary>
    public SKPoint[] Vertices { get; }

    /// <summary>
    /// Gets the border effect applied to this annotation's border, or null for no effect.
    /// </summary>
    public PdfBorderEffect? BorderEffect { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (Vertices.Length < 3)
        {
            return false;
        }

        SKPath path = new();

        path.MoveTo(Vertices[0]);

        for (int i = 1; i < Vertices.Length; i++)
        {
            path.LineTo(Vertices[i]);
        }

        path.Close();

        SKColor interiorSKColor = ResolveInteriorColor(page);
        bool hasStroke = BorderStyle?.Width > 0 && Color?.Length > 0;

        if (interiorSKColor != SKColors.Transparent)
        {
            SKPaint fillPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = interiorSKColor
            };

            processor.Process(new DrawPathCommand(hasStroke ? new SKPath(path) : path, fillPaint));
        }

        if (BorderStyle != null && hasStroke)
        {
            SKColor strokeColor = ResolveColor(page, SKColors.Black);

            SKPaint strokePaint = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderStyle.Width,
                StrokeJoin = SKStrokeJoin.Miter,
                Color = strokeColor
            };

            BorderStyle.TryApplyEffect(strokePaint, strokeColor);
            BorderEffect?.TryApplyEffect(strokePaint, BorderStyle.Width);

            processor.Process(new DrawPathCommand(path, strokePaint));
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this polygon annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Polygon Annotation: {contentsText}";
        }

        return "Polygon Annotation";
    }
}
