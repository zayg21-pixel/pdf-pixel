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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind, PdfRenderingParameters renderingParameters)
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
                IsAntialias = renderingParameters.Antialias
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
