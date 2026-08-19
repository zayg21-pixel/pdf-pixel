using PdfPixel.Annotations.Models;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Contains information about an annotation popup.
/// </summary>
public class PdfAnnotationPopup
{
    /// <summary>
    /// Initializes a popup with navigation data, a pre-computed hover rectangle, and messages.
    /// Used when the document is not directly accessible — for example, on the WASM main thread
    /// after annotation data has been serialized from the worker.
    /// </summary>
    public PdfAnnotationPopup(PdfAnnotationNavigation? navigation, bool isInteractive, in PdfRectangle hoverRectangle, PdfAnnotationMessage[] messages)
    {
        Navigation = navigation;
        IsInteractive = isInteractive;
        HoverRectangle = hoverRectangle;
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    /// <summary>
    /// Initializes a popup backed by a live page annotation and pre-built navigation.
    /// Used when the document is loaded in-process (WPF path).
    /// </summary>
    internal PdfAnnotationPopup(PdfPageAnnotation pageAnnotation, PdfAnnotationNavigation? navigation, PdfAnnotationMessage[] messages)
        : this(navigation, pageAnnotation.Content.IsInteractive, pageAnnotation.GetHoverRectangle(), messages)
    {
        PageAnnotation = pageAnnotation ?? throw new ArgumentNullException(nameof(pageAnnotation));
    }

    /// <summary>
    /// The page-bound annotation this popup represents.
    /// Available only when the document is loaded in-process. Null on the WASM main thread.
    /// </summary>
    internal PdfPageAnnotation? PageAnnotation { get; }

    /// <summary>
    /// Whether this annotation accepts pointer interaction (hit-testing, cursor change, click).
    /// </summary>
    public bool IsInteractive { get; }

    /// <summary>
    /// Hover rectangle in PDF coordinates. Used for hit-testing.
    /// </summary>
    public PdfRectangle HoverRectangle { get; }

    /// <summary>
    /// Navigation data for this annotation (navigation type, URI, destination, cursor type).
    /// </summary>
    public PdfAnnotationNavigation? Navigation { get; }

    /// <summary>
    /// Thread of annotation messages (from oldest to newest).
    /// First message is the root annotation, subsequent messages are replies in chronological order.
    /// </summary>
    public PdfAnnotationMessage[] Messages { get; }
}
