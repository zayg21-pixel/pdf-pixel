using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations;

/// <summary>
/// Factory class for creating PDF annotation instances from PDF objects.
/// </summary>
/// <remarks>
/// This factory examines the annotation's subtype and creates the appropriate
/// concrete annotation class instance. If the subtype is not recognized,
/// it returns a generic annotation wrapper.
/// </remarks>
public static class PdfAnnotationFactory
{
    /// <summary>
    /// Creates an annotation instance from a PDF object.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing an annotation.</param>
    /// <returns>A concrete annotation instance, or null if the object is not a valid annotation.</returns>
    public static PdfAnnotationBase CreateAnnotation(PdfObject annotationObject)
    {
        if (annotationObject == null)
        {
            return null;
        }

        // Get the annotation subtype and convert to enum
        var subtype = annotationObject.Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfAnnotationSubType>();
        if (subtype == PdfAnnotationSubType.Unknown)
        {
            return null; // Not a valid annotation without a valid subtype
        }

        // Create specific annotation types based on subtype
        return subtype switch
        {
            PdfAnnotationSubType.Text => new PdfTextAnnotation(annotationObject),
            PdfAnnotationSubType.Link => new PdfLinkAnnotation(annotationObject),
            PdfAnnotationSubType.FreeText => new PdfFreeTextAnnotation(annotationObject),
            PdfAnnotationSubType.Line => new PdfLineAnnotation(annotationObject),
            PdfAnnotationSubType.Square => new PdfSquareAnnotation(annotationObject),
            PdfAnnotationSubType.Circle => new PdfCircleAnnotation(annotationObject),
            PdfAnnotationSubType.Polygon => new PdfPolygonAnnotation(annotationObject),
            PdfAnnotationSubType.PolyLine => new PdfPolyLineAnnotation(annotationObject),
            PdfAnnotationSubType.Highlight => new PdfHighlightAnnotation(annotationObject),
            PdfAnnotationSubType.Underline => new PdfUnderlineAnnotation(annotationObject),
            PdfAnnotationSubType.Squiggly => new PdfSquigglyAnnotation(annotationObject),
            PdfAnnotationSubType.StrikeOut => new PdfStrikeOutAnnotation(annotationObject),
            // Caret, Stamp, FileAttachment
            PdfAnnotationSubType.Ink => new PdfInkAnnotation(annotationObject),
            PdfAnnotationSubType.Popup => new PdfPopupAnnotation(annotationObject),
            //PdfAnnotationSubType.Widget => new PdfWidgetAnnotation(annotationObject), // Widget is not fully implemented yet
            // TODO: Add more annotation types as they are implemented
            // etc.
            _ => new PdfGenericAnnotation(annotationObject, subtype)
        };
    }

    /// <summary>
    /// Checks if a PDF object represents a valid annotation.
    /// </summary>
    /// <param name="pdfObject">The PDF object to check.</param>
    /// <returns>True if the object is a valid annotation, false otherwise.</returns>
    public static bool IsAnnotation(PdfObject pdfObject)
    {
        if (pdfObject == null)
        {
            return false;
        }

        // Check if the Type is /Annot
        var type = pdfObject.Dictionary.GetName(PdfTokens.TypeKey);
        if (!type.IsEmpty && type == PdfTokens.AnnotationKey)
        {
            return true;
        }

        // Some annotations might not have the Type field set, so check for Subtype
        var subtype = pdfObject.Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfAnnotationSubType>();
        return subtype != PdfAnnotationSubType.Unknown;
    }
}