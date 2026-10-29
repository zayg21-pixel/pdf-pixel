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
    {
        PdfAnnotation annotation = PageAnnotation.Content;
        int rotation = _panelDocument.Pages[PageAnnotation.Page.PageNumber - 1].Rotation;
        PdfMatrix displayMatrix = annotation.GetDisplayMatrix(scale, referenceScale, rotation);
        PdfPoint annotationPoint = displayMatrix.Invert().MapPoint(pdfPoint);
        float displayScale = (annotation.IsNoZoom()) ? referenceScale : scale;
        float tolerance = HoverTolerance / (displayScale * PageAnnotation.Page.UserUnit);

        return PageAnnotation.GetHoverPath(tolerance).Contains(annotationPoint)
            || (annotation.ShouldDisplayBubble && IsOverBubble(annotationPoint, scale, referenceScale, rotation));
    }

    private bool IsOverBubble(in PdfPoint annotationPoint, float scale, float referenceScale, int rotation)
    {
        PdfPoint bubbleAnchor = PageAnnotation.Content.ContentStart;
        PdfMatrix bubbleMatrix = PageAnnotation.Content.GetBubbleMatrix(bubbleAnchor, scale, referenceScale, rotation, PageAnnotation.Page.CropBox);

        return PdfAnnotation.GetBubbleRectangle(bubbleAnchor).Contains(bubbleMatrix.Invert().MapPoint(annotationPoint));
    }
}
