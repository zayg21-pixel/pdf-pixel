using PdfPixel.Commands;
using PdfPixel.Models;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a generic PDF annotation for annotation types that don't have
/// a specific implementation yet.
/// </summary>
/// <remarks>
/// This class provides access to the common annotation properties defined in
/// <see cref="PdfAnnotationBase"/> for annotation subtypes that are not yet
/// specifically implemented in the library.
/// </remarks>
public class PdfGenericAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGenericAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this annotation.</param>
    /// <param name="subtype">The annotation subtype.</param>
    public PdfGenericAnnotation(PdfObject annotationObject, PdfAnnotationSubType subtype)
        : base(annotationObject, subtype)
    {
    }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
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
        if (Contents?.IsEmpty == false)
        {
            return $"{Subtype} Annotation: {Contents}";
        }

        if (Name?.IsEmpty == false)
        {
            return $"{Subtype} Annotation: {Name}";
        }

        return $"{Subtype} Annotation";
    }
}
