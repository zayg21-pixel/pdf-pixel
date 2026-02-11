using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a free-form text annotation in a PDF document that allows users to add arbitrary text content.
/// </summary>
/// <remarks>Free-form text annotations do not display a bubble and require an appearance stream for rendering.
/// Fallback rendering is not implemented, as these annotations should always provide an appearance stream to ensure
/// correct display.</remarks>
public class PdfFreeTextAnnotation : PdfAnnotationBase
{
    public PdfFreeTextAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.FreeText)
    {
    }

    public override bool ShouldDisplayBubble => false;

    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        // FreeText annotations should always have an appearance stream, so fallback rendering is not implemented.
        return null;
    }
}
