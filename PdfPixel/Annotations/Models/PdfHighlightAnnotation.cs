using PdfPixel.Commands;
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
    /// Renders the appearance stream for this highlight annotation with multiply blend mode
    /// by recording commands and replaying them through a <see cref="BlendModePaintModifier"/>.
    /// </summary>
    protected override bool RenderAppearanceStream(
        IPdfCommandProcessor processor,
        PdfPage page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        var recorder = new PdfCommandRecorder();
        var recorded = base.RenderAppearanceStream(recorder, page, visualStateKind, renderer, renderingParameters, observer);

        if (!recorded)
        {
            recorder.Dispose();
            return false;
        }

        var modifier = new BlendModePaintModifier(SKBlendMode.Multiply);
        processor.Process(new DrawRecordingCommand(recorder, modifier));
        return true;
    }

    /// <summary>
    /// Renders the fallback content for highlight annotations when no appearance stream is available.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>True if fallback rendering was emitted.</returns>
    public override bool RenderFallback(IPdfCommandProcessor processor, PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        var quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        var color = ResolveColor(page, new SKColor(255, 255, 0));

        foreach (var quad in quads)
        {
            using var path = new SKPath();
            path.MoveTo(quad[0]);
            path.LineTo(quad[1]);
            path.LineTo(quad[2]);
            path.LineTo(quad[3]);
            path.Close();

            var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = color,
                IsAntialias = true,
                BlendMode = SKBlendMode.Multiply
            };

            processor.Process(new DrawPathCommand(path, paint));
        }

        return true;
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
