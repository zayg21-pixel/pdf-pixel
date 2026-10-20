using PdfPixel.Annotations.Model;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent.Model;
using PdfPixel.PdfPanel.ContentProvider;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Contains information about an annotation popup.
/// </summary>
public class PdfAnnotationPopup
{
    private readonly PdfPanelDocument _panelDocument;

    /// <summary>
    /// Initializes a popup backed by a live page annotation.
    /// </summary>
    internal PdfAnnotationPopup(PdfPanelDocument panelDocument, PdfPageAnnotation pageAnnotation, PdfAnnotationMessage[] messages)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        PageAnnotation = pageAnnotation ?? throw new ArgumentNullException(nameof(pageAnnotation));
        HoverRectangles = pageAnnotation.GetHoverRectangles();
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
            PdfAnnotationBase annotation = PageAnnotation.Content;
            PdfOptionalContentStates? optionalContentStates = _panelDocument.OptionalContentStates;

            return annotation.IsInteractive
                && !annotation.IsHidden
                && (annotation.OptionalContent == null
                    || optionalContentStates == null
                    || optionalContentStates.IsVisible(annotation.OptionalContent));
        }
    }

    /// <summary>
    /// Hover rectangles in PDF coordinates. Used for hit-testing.
    /// </summary>
    public IReadOnlyList<PdfRectangle> HoverRectangles { get; }

    /// <summary>
    /// Thread of annotation messages (from oldest to newest).
    /// First message is the root annotation, subsequent messages are replies in chronological order.
    /// </summary>
    public IReadOnlyList<PdfAnnotationMessage> Messages { get; }
}
