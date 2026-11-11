using PdfPixel.Annotations;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Contains information about an annotation popup.
/// </summary>
public class PdfAnnotationPopup
{
    // Distance in device pixels the hover area reaches beyond what is drawn.
    private const float HoverTolerance = 3f;

    private readonly PdfPanelDocument _panelDocument;

    /// <summary>
    /// Initializes a popup backed by a live page annotation.
    /// </summary>
    internal PdfAnnotationPopup(PdfPanelDocument panelDocument, PdfPageAnnotation pageAnnotation, PdfAnnotationMessage[] messages)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        PageAnnotation = pageAnnotation ?? throw new ArgumentNullException(nameof(pageAnnotation));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    /// <summary>
    /// The page-bound annotation this popup represents.
    /// </summary>
    public PdfPageAnnotation PageAnnotation { get; }

    /// <summary>
    /// Whether this annotation accepts pointer interaction (hit-testing, cursor change, click).
    /// </summary>
    public bool IsInteractive
    {
        get
        {
            PdfAnnotation annotation = PageAnnotation.Content;
            PdfOptionalContentStates? optionalContentStates = _panelDocument.OptionalContentStates;

            return annotation.IsInteractive()
                && (annotation.OptionalContent == null
                    || optionalContentStates == null
                    || optionalContentStates.IsVisible(annotation.OptionalContent));
        }
    }

    /// <summary>
    /// Thread of annotation messages (from oldest to newest).
    /// First message is the root annotation, subsequent messages are replies in chronological order.
    /// </summary>
    public IReadOnlyList<PdfAnnotationMessage> Messages { get; }

    /// <summary>
    /// Gets whether <paramref name="pdfPoint"/>, in PDF coordinates, is over the annotation or its bubble at the given
    /// scale and the scale of a zoom of 100%. Used for hit-testing.
    /// </summary>
    public bool HitTest(in PdfPoint pdfPoint, float scale, float referenceScale)
        => IsOverContent(pdfPoint, scale, referenceScale) || IsOverBubble(pdfPoint, scale, referenceScale);

    /// <summary>
    /// Gets whether <paramref name="pdfPoint"/>, in PDF coordinates, is over the annotation itself, not its bubble, at the
    /// given scale and the scale of a zoom of 100%.
    /// </summary>
    public bool IsOverContent(in PdfPoint pdfPoint, float scale, float referenceScale)
    {
        PdfAnnotation annotation = PageAnnotation.Content;
        (PdfPoint annotationPoint, _) = ToAnnotationPoint(pdfPoint, scale, referenceScale);
        float displayScale = (annotation.IsNoZoom()) ? referenceScale : scale;
        float tolerance = HoverTolerance / (displayScale * PageAnnotation.Page.UserUnit);

        return PageAnnotation.GetHoverPath(tolerance).Contains(annotationPoint);
    }

    /// <summary>
    /// Gets whether <paramref name="pdfPoint"/>, in PDF coordinates, is over the bubble of the annotation at the given
    /// scale and the scale of a zoom of 100%; never when it shows none.
    /// </summary>
    public bool IsOverBubble(in PdfPoint pdfPoint, float scale, float referenceScale)
    {
        PdfAnnotation annotation = PageAnnotation.Content;

        if (!annotation.ShouldDisplayBubble())
        {
            return false;
        }

        (PdfPoint annotationPoint, int rotation) = ToAnnotationPoint(pdfPoint, scale, referenceScale);
        PdfPoint bubbleAnchor = annotation.ContentStart;
        PdfMatrix bubbleMatrix = annotation.GetBubbleMatrix(bubbleAnchor, scale, referenceScale, rotation, PageAnnotation.Page.CropBox);

        return PdfAnnotationExtensions.GetBubbleRectangle(bubbleAnchor).Contains(bubbleMatrix.Invert().MapPoint(annotationPoint));
    }

    /// <summary>
    /// Returns <paramref name="pdfPoint"/> in the space the annotation is drawn in at the given scale and the scale of a
    /// zoom of 100%, and the rotation of its page.
    /// </summary>
    private (PdfPoint AnnotationPoint, int Rotation) ToAnnotationPoint(in PdfPoint pdfPoint, float scale, float referenceScale)
    {
        int rotation = _panelDocument.Pages[PageAnnotation.Page.PageNumber - 1].Rotation;
        PdfMatrix displayMatrix = PageAnnotation.Content.GetDisplayMatrix(scale, referenceScale, rotation);

        return (displayMatrix.Invert().MapPoint(pdfPoint), rotation);
    }
}
