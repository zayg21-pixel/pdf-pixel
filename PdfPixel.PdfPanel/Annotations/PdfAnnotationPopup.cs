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
    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfRectangle[] _hoverRectangles;

    /// <summary>
    /// Initializes a popup backed by a live page annotation.
    /// </summary>
    internal PdfAnnotationPopup(PdfPanelDocument panelDocument, PdfPageAnnotation pageAnnotation, PdfAnnotationMessage[] messages)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        PageAnnotation = pageAnnotation ?? throw new ArgumentNullException(nameof(pageAnnotation));

        if (pageAnnotation.Content.ShouldDisplayBubble)
        {
            _hoverRectangles = [.. pageAnnotation.GetHoverRectangles(), pageAnnotation.GetBubbleRectangle(pageAnnotation.Content.ContentStart)];
        }
        else
        {
            _hoverRectangles = pageAnnotation.GetHoverRectangles();
        }

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
    /// Gets the hover rectangles in PDF coordinates at the given scale and the scale of a zoom of 100%.
    /// Used for hit-testing.
    /// </summary>
    public IReadOnlyList<PdfRectangle> GetHoverRectangles(float scale, float referenceScale)
    {
        int rotation = _panelDocument.Pages[PageAnnotation.Page.PageNumber - 1].Rotation;
        PdfMatrix displayMatrix = PageAnnotation.Content.GetDisplayMatrix(scale, referenceScale, rotation);
        var hoverRectangles = new PdfRectangle[_hoverRectangles.Length];

        for (int rectangleIndex = 0; rectangleIndex < _hoverRectangles.Length; rectangleIndex++)
        {
            hoverRectangles[rectangleIndex] = displayMatrix.MapRect(_hoverRectangles[rectangleIndex]);
        }

        return hoverRectangles;
    }
}
