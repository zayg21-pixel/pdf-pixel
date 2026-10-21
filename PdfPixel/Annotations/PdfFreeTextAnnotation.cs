using PdfPixel.Commands;
using PdfPixel.Objects;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a free-form text annotation in a PDF document that allows users to add arbitrary text content.
/// </summary>
/// <remarks>Free-form text annotations do not display a bubble and require an appearance stream for rendering.
/// Fallback rendering is not implemented, as these annotations should always provide an appearance stream to ensure
/// correct display.</remarks>
public class PdfFreeTextAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFreeTextAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this free text annotation.</param>
    public PdfFreeTextAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.FreeText)
    {
        // TODO: [MEDIUM] Not parsed, so a FreeText without an appearance stream is not drawn:
        // - default appearance string: font, size and text color (DA)
        // - text justification: left, centered or right (Q)
        // - callout line points and its line ending, not drawn for the typewriter intent (CL, LE)
        // - inset of the text box from the annotation rectangle (RD)
        // - default rich text style string (DS)
        // - border effect, cloudy border (BE)
        // Fallback rendering waits on default appearance handling from the Widget implementation.
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        // FreeText annotations should always have an appearance stream, so fallback rendering is not implemented.
        return false;
    }
}
