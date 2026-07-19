using PdfPixel.Commands;
using PdfPixel.Geometry;
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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        PdfPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        SKColor color = ResolveColor(page, SKColors.Red);

        foreach (PdfPoint[] quad in quads)
        {
            float startX = (quad[0].X + quad[3].X) / 2;
            float startY = (quad[0].Y + quad[3].Y) / 2;
            float endX = (quad[1].X + quad[2].X) / 2;
            float endY = (quad[1].Y + quad[2].Y) / 2;

            SKPaint paint = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                Color = color
            };

            PdfPath linePath = new();
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
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"StrikeOut Annotation: {contentsText}";
        }

        return "StrikeOut Annotation";
    }
}
