using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF strikeout annotation.
/// </summary>
/// <remarks>
/// Strikeout annotations mark text with a line drawn through the middle of it,
/// typically used to indicate deleted or obsolete content.
/// </remarks>
public class PdfStrikeOutAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfStrikeOutAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this strikeout annotation.</param>
    public PdfStrikeOutAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.StrikeOut)
    {
    }

    /// <summary>
    /// Creates a fallback rendering for strikeout annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered strikeout line.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        var quads = GetQuadrilaterals();
        if (quads.Length == 0)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var color = ResolveColor(page, SKColors.Red);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            Color = color,
            IsAntialias = true
        };

        foreach (var quad in quads)
        {
            var startX = (quad[0].X + quad[3].X) / 2;
            var startY = (quad[0].Y + quad[3].Y) / 2;
            var endX = (quad[1].X + quad[2].X) / 2;
            var endY = (quad[1].Y + quad[2].Y) / 2;

            canvas.DrawLine(startX, startY, endX, endY, paint);
        }

        return recorder.EndRecording();
    }

    /// <summary>
    /// Returns a string representation of this strikeout annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"StrikeOut Annotation: {contentsText}";
        }

        return "StrikeOut Annotation";
    }
}
