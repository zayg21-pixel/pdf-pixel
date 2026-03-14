using PdfPixel.Commands;
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
    /// Renders the fallback content for strikeout annotations when no appearance stream is available.
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

        var color = ResolveColor(page, SKColors.Red);

        foreach (var quad in quads)
        {
            var startX = (quad[0].X + quad[3].X) / 2;
            var startY = (quad[0].Y + quad[3].Y) / 2;
            var endX = (quad[1].X + quad[2].X) / 2;
            var endY = (quad[1].Y + quad[2].Y) / 2;

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
