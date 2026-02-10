using PdfPixel.Models;
using SkiaSharp;

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

    /// <summary>
    /// Creates a fallback rendering for generic annotations.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <returns>Null - generic annotations don't have specific fallback rendering.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page)
    {
        // Generic annotations don't provide custom fallback rendering
        return null;
    }

    /// <summary>
    /// Returns a string representation of this generic annotation.
    /// </summary>
    /// <returns>A string containing the annotation subtype and basic information.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();
        
        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"{Subtype} Annotation: {contentsText}";
        }
        
        var nameText = Name.ToString();
        
        if (!string.IsNullOrEmpty(nameText))
        {
            return $"{Subtype} Annotation: {nameText}";
        }
        
        return $"{Subtype} Annotation";
    }
}