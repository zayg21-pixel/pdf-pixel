using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF highlight annotation.
/// </summary>
public class PdfHighlightAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfHighlightAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this highlight annotation.</param>
    internal PdfHighlightAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Highlight)
    {
    }

    /// <inheritdoc/>
    protected override bool UsesOpacityLayer => false;

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        PdfColor color = ResolveColor(page);
        PdfPathBuilder path = new();
        path.AddQuadrilaterals(quads);

        PdfPaint paint = PdfAnnotationPaintFactory.CreateHighlightPaint(color, Opacity);

        processor.Process(new DrawPathCommand(path.ToPath(), paint));

        return true;
    }

    /// <summary>
    /// Returns a string representation of this highlight annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Highlight Annotation: {Contents}";
        }

        return "Highlight Annotation";
    }
}
