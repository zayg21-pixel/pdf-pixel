using PdfPixel.Annotations.Model;
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
internal static class PdfAnnotationFactory
{
    /// <summary>
    /// The annotation at <paramref name="index"/> of <paramref name="array"/>, or <see langword="null"/> when absent.
    /// </summary>
    public static PdfAnnotationBase? GetAnnotation(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, CreateAnnotation);

    /// <summary>
    /// Creates an annotation instance from a PDF object.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing an annotation.</param>
    /// <returns>A concrete annotation instance, or null if the object is not a valid annotation.</returns>
    private static PdfAnnotationBase? CreateAnnotation(PdfObject annotationObject)
    {
        if (annotationObject == null)
        {
            return null;
        }

        // Get the annotation subtype and convert to enum
        PdfAnnotationSubType subtype = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfAnnotationSubType>();

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
            PdfAnnotationSubType.Stamp => new PdfStampAnnotation(annotationObject),
            PdfAnnotationSubType.Caret => new PdfCaretAnnotation(annotationObject),
            PdfAnnotationSubType.Ink => new PdfInkAnnotation(annotationObject),
            PdfAnnotationSubType.Popup => new PdfPopupAnnotation(annotationObject),
            PdfAnnotationSubType.FileAttachment => new PdfFileAttachmentAnnotation(annotationObject),
            PdfAnnotationSubType.Widget => new PdfWidgetAnnotation(annotationObject), // TODO: [LOW] Widget is not fully implemented yet
            _ => new PdfGenericAnnotation(annotationObject, subtype)
        };
    }
}
