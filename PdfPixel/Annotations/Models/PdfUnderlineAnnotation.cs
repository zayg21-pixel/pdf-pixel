using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF underline annotation.
/// </summary>
/// <remarks>
/// Underline annotations mark text with a line drawn under it.
/// </remarks>
public class PdfUnderlineAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfUnderlineAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this underline annotation.</param>
    public PdfUnderlineAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Underline)
    {
    }

    /// <summary>
    /// Creates a fallback rendering for underline annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered underline.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        var quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var color = ResolveColor(page, SKColors.Black);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            Color = color,
            IsAntialias = true
        };

        foreach (var quad in quads)
        {
            var startX = quad[0].X;
            var startY = quad[0].Y;
            var endX = quad[1].X;
            var endY = quad[1].Y;

            canvas.DrawLine(startX, startY, endX, endY, paint);
        }

        return recorder.EndRecording();
    }

    /// <summary>
    /// Returns a string representation of this underline annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Underline Annotation: {contentsText}";
        }

        return "Underline Annotation";
    }
}
