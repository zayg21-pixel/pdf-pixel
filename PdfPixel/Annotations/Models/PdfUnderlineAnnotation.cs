using PdfPixel.Commands;
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
    /// Renders the fallback content for underline annotations when no appearance stream is available.
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

        var color = ResolveColor(page, SKColors.Black);

        foreach (var quad in quads)
        {
            var startX = quad[0].X;
            var startY = quad[0].Y;
            var endX = quad[1].X;
            var endY = quad[1].Y;

            var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                Color = color,
                IsAntialias = true
            };

            using var linePath = new SKPath();
            linePath.MoveTo(startX, startY);
            linePath.LineTo(endX, endY);
            processor.Process(new DrawPathCommand(linePath, paint));
        }

        return true;
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
