using PdfPixel.Models;
using PdfPixel.Rendering;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF highlight annotation.
/// </summary>
/// <remarks>
/// Highlight annotations mark text with a semi-transparent colored background,
/// typically yellow, to draw attention to specific content.
/// </remarks>
public class PdfHighlightAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfHighlightAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this highlight annotation.</param>
    public PdfHighlightAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Highlight)
    {
    }

    /// <summary>
    /// Renders this highlight annotation with multiply blend mode for proper color blending.
    /// </summary>
    public override bool Render(
        SKCanvas canvas,
        PdfPage page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters)
    {
        using var paint = new SKPaint
        {
            BlendMode = SKBlendMode.Multiply
        };
        canvas.SaveLayer(Rectangle, paint);

        try
        {
            return base.Render(canvas, page, visualStateKind, renderer, renderingParameters);
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>
    /// Creates a fallback rendering for highlight annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered highlight.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        var quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var color = ResolveColor(page, new SKColor(255, 255, 0));

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = color,
            IsAntialias = true
        };

        foreach (var quad in quads)
        {
            using var path = new SKPath();
            path.MoveTo(quad[0]);
            path.LineTo(quad[1]);
            path.LineTo(quad[2]);
            path.LineTo(quad[3]);
            path.Close();

            canvas.DrawPath(path, paint);
        }

        return recorder.EndRecording();
    }

    /// <summary>
    /// Returns a string representation of this highlight annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Highlight Annotation: {contentsText}";
        }

        return "Highlight Annotation";
    }
}
