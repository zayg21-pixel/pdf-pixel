using PdfPixel.Color.ColorSpace;
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
        var vertices = annotationObject.Dictionary.GetArray(PdfTokens.VerticesKey)?.GetFloatArray();
        
        if (vertices != null)
        {
            Vertices = new SKPoint[vertices.Length / 2];

            for (int i = 0; i < vertices.Length; i += 2)
            {
                Vertices[i / 2] = new SKPoint(vertices[i], vertices[i + 1]);
            }
        }
    }

    protected override SKPoint ContentStart => Vertices?.Length > 0 ? Vertices[0] : base.ContentStart;

    /// <summary>
    /// Gets the vertices array containing coordinates of the polygon vertices.
    /// </summary>
    public SKPoint[] Vertices { get; }

    /// <summary>
    /// Creates a fallback rendering for polygon annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered polygon.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (Vertices == null || Vertices.Length < 3)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        using var path = new SKPath();

        path.MoveTo(Vertices[0]);

        for (int i = 1; i < Vertices.Length; i++)
        {
            path.LineTo(Vertices[i]);
        }

        path.Close();

        var interiorSKColor = ResolveInteriorColor(page);
        if (interiorSKColor != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = interiorSKColor
            };

            canvas.DrawPath(path, fillPaint);
        }

        if (BorderStyle != null && BorderStyle.Width > 0 && Color != null && Color.Length > 0)
        {
            var strokeColor = ResolveColor(page, SKColors.Black);

            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderStyle.Width,
                StrokeJoin = SKStrokeJoin.Miter,
                IsAntialias = true,
                Color = strokeColor
            };

            BorderStyle.TryApplyEffect(strokePaint, strokeColor);

            canvas.DrawPath(path, strokePaint);
        }

        return recorder.EndRecording();
    }

    /// <summary>
    /// Returns a string representation of this polygon annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Polygon Annotation: {contentsText}";
        }

        return "Polygon Annotation";
    }
}
