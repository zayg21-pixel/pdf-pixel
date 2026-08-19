using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;

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
        PdfPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        PdfColor color = ResolveColor(page, PdfColors.Black);

        foreach (PdfPoint[] quad in quads)
        {
            const float baselineOffset = 1.3f;

            float startX = quad[0].X;
            float startY = quad[0].Y + baselineOffset;
            float endX = quad[1].X;
            float endY = quad[1].Y + baselineOffset;

            PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color);

            PdfPathBuilder linePath = new();
            linePath.MoveTo(startX, startY);
            linePath.LineTo(endX, endY);
            processor.Process(new DrawPathCommand(linePath.ToPath(), paint));
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this underline annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Underline Annotation: {Contents}";
        }

        return "Underline Annotation";
    }
}
