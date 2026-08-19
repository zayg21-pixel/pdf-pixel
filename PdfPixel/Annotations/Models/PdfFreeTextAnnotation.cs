using PdfPixel.Commands.Model;
using PdfPixel.Models;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a free-form text annotation in a PDF document that allows users to add arbitrary text content.
/// </summary>
/// <remarks>Free-form text annotations do not display a bubble and require an appearance stream for rendering.
/// Fallback rendering is not implemented, as these annotations should always provide an appearance stream to ensure
/// correct display.</remarks>
public class PdfFreeTextAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFreeTextAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this free text annotation.</param>
    public PdfFreeTextAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.FreeText)
    {
        // TODO: [MEDIUM] DA (default appearance), Q (justification), IT (intent), CL (callout line), RD (rect inset), DS, BE
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        // FreeText annotations should always have an appearance stream, so fallback rendering is not implemented.
        return false;
    }
}
