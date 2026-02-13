using PdfPixel.Annotations.Rendering;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

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
        var vertices = annotationObject.Dictionary.GetArray(PdfTokens.VerticesKey)?.GetFloatArray();

        if (vertices != null)
        {
            Vertices = new SKPoint[vertices.Length / 2];

            for (int i = 0; i < vertices.Length; i += 2)
            {
                Vertices[i / 2] = new SKPoint(vertices[i], vertices[i + 1]);
            }
        }

        var lineEndingArray = annotationObject.Dictionary.GetArray(PdfTokens.LineEndingKey);
        if (lineEndingArray != null && lineEndingArray.Count >= 2)
        {
            StartLineEnding = lineEndingArray.GetName(0).AsEnum<PdfLineEndingStyle>();
            EndLineEnding = lineEndingArray.GetName(1).AsEnum<PdfLineEndingStyle>();
        }
    }

    protected override SKPoint ContentStart => Vertices != null && Vertices.Length > 0 ? Vertices[0] : base.ContentStart;

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
    /// Creates a fallback rendering for polyline annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered polyline.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (Vertices == null || Vertices.Length < 2)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var lineColor = ResolveColor(page, SKColors.Black);
        var lineWidth = BorderStyle?.Width ?? 1.0f;

        using var path = new SKPath();

        path.MoveTo(Vertices[0]);

        for (int i = 1; i < Vertices.Length; i++)
        {
            path.LineTo(Vertices[i]);
        }

        using var linePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            StrokeJoin = SKStrokeJoin.Miter,
            StrokeCap = SKStrokeCap.Butt,
            IsAntialias = true,
            Color = lineColor
        };

        BorderStyle?.TryApplyEffect(linePaint, lineColor);

        canvas.DrawPath(path, linePaint);

        var interiorSKColor = ResolveInteriorColor(page);

        if (StartLineEnding != PdfLineEndingStyle.None && Vertices.Length >= 4)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                canvas,
                Vertices[0].X,
                Vertices[0].Y,
                Vertices[1].X,
                Vertices[1].Y,
                StartLineEnding,
                lineWidth,
                lineColor,
                interiorSKColor);
        }

        if (EndLineEnding != PdfLineEndingStyle.None && Vertices.Length >= 4)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                canvas,
                Vertices[Vertices.Length - 1].X,
                Vertices[Vertices.Length - 1].Y,
                Vertices[Vertices.Length - 2].X,
                Vertices[Vertices.Length - 2].Y,
                EndLineEnding,
                lineWidth,
                lineColor,
                interiorSKColor);
        }

        return recorder.EndRecording();
    }

    /// <summary>
    /// Returns a string representation of this polyline annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"PolyLine Annotation: {contentsText}";
        }

        return "PolyLine Annotation";
    }
}
