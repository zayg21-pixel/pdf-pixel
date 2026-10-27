using PdfPixel.Commands;
using PdfPixel.Objects;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a generic PDF annotation for annotation types that don't have
/// a specific implementation yet.
/// </summary>
public class PdfGenericAnnotation : PdfAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGenericAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this annotation.</param>
    /// <param name="subtype">The annotation subtype.</param>
    internal PdfGenericAnnotation(PdfObject annotationObject, PdfAnnotationSubType subtype)
        : base(annotationObject, subtype)
    {
    }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        // Generic annotations don't provide custom fallback rendering
        return false;
    }

    /// <summary>
    /// Returns a string representation of this generic annotation.
    /// </summary>
    /// <returns>A string containing the annotation subtype and basic information.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"{Subtype} Annotation: {Contents}";
        }

        if (Name != null)
        {
            return $"{Subtype} Annotation: {Name}";
        }

        return $"{Subtype} Annotation";
    }
}
