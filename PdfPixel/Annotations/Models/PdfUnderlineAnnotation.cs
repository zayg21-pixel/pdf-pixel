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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        SKPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        SKColor color = ResolveColor(page, SKColors.Black);

        foreach (SKPoint[] quad in quads)
        {
            float startX = quad[0].X;
            float startY = quad[0].Y;
            float endX = quad[1].X;
            float endY = quad[1].Y;

            SKPaint paint = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                Color = color
            };

            using SKPathBuilder linePathBuilder = new();
            linePathBuilder.MoveTo(startX, startY);
            linePathBuilder.LineTo(endX, endY);
            processor.Process(new DrawPathCommand(linePathBuilder.Detach(), paint));
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this underline annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Underline Annotation: {contentsText}";
        }

        return "Underline Annotation";
    }
}
