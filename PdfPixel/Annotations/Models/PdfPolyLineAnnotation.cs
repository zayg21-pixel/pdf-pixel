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
        Vertices = annotationObject.Dictionary.GetArray(PdfTokens.VerticesKey)?.GetFloatArray();

        var lineEndingArray = annotationObject.Dictionary.GetArray(PdfTokens.LineEndingKey);
        if (lineEndingArray != null && lineEndingArray.Count >= 2)
        {
            StartLineEnding = lineEndingArray.GetName(0).AsEnum<PdfLineEndingStyle>();
            EndLineEnding = lineEndingArray.GetName(1).AsEnum<PdfLineEndingStyle>();
        }
    }

    /// <summary>
    /// Gets the vertices array containing alternating x,y coordinates of the polyline vertices.
    /// </summary>
    public float[] Vertices { get; }

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
        if (Vertices == null || Vertices.Length < 4)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var lineColor = ResolveColor(page, SKColors.Black);
        var lineWidth = BorderStyle?.Width ?? 1.0f;

        using var path = new SKPath();

        path.MoveTo(Vertices[0], Vertices[1]);

        for (int i = 2; i < Vertices.Length; i += 2)
        {
            if (i + 1 < Vertices.Length)
            {
                path.LineTo(Vertices[i], Vertices[i + 1]);
            }
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
                Vertices[0],
                Vertices[1],
                Vertices[2],
                Vertices[3],
                StartLineEnding,
                lineWidth,
                lineColor,
                interiorSKColor);
        }

        if (EndLineEnding != PdfLineEndingStyle.None && Vertices.Length >= 4)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                canvas,
                Vertices[Vertices.Length - 2],
                Vertices[Vertices.Length - 1],
                Vertices[Vertices.Length - 4],
                Vertices[Vertices.Length - 3],
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
