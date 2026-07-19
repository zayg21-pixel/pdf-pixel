using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Geometry;
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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        PdfPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        SKColor color = ResolveColor(page, new SKColor(255, 255, 0));

        foreach (PdfPoint[] quad in quads)
        {
            PdfPath path = new();
            path.MoveTo(quad[0]);
            path.LineTo(quad[1]);
            path.LineTo(quad[2]);
            path.LineTo(quad[3]);
            path.Close();

            SKPaint paint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = color,
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
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Highlight Annotation: {contentsText}";
        }

        return "Highlight Annotation";
    }
}
