using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Text;

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

    /// <inheritdoc/>
    /// <remarks>
    /// The Multiply blend needs the marked content as its backdrop, which a layer would hide, so the
    /// highlight paints its opacity directly instead.
    /// </remarks>
    protected override bool UsesOpacityLayer => false;

    /// <inheritdoc/>
    /// <remarks>
    /// A highlight has to darken the content it marks, which takes a blend mode and so an ExtGState. An
    /// appearance stream declaring no ExtGState in its resources cannot be blending, and would paint over
    /// that content instead of through it, so it is discarded in favour of the fallback rendering.
    /// </remarks>
    internal override bool RenderAppearanceStream(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfObject? appearanceObject = Appearance?.GetStream(visualStateKind);
        PdfDictionary? resources = appearanceObject?.Dictionary.GetDictionary(PdfTokens.ResourcesKey);

        if (resources == null || !resources.HasKey(PdfTokens.ExtGStateKey))
        {
            return false;
        }

        return base.RenderAppearanceStream(processor, page, visualStateKind, renderer, renderingParameters, observer);
    }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        PdfPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        PdfColor color = ResolveColor(page, PdfColors.Yellow);

        foreach (PdfPoint[] quad in quads)
        {
            PdfPathBuilder path = new();
            path.MoveTo(quad[0]);
            path.LineTo(quad[1]);
            path.LineTo(quad[2]);
            path.LineTo(quad[3]);
            path.Close();

            PdfPaint paint = PdfAnnotationPaintFactory.CreateHighlightPaint(color, Opacity);

            processor.Process(new DrawPathCommand(path.ToPath(), paint));
        }

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
