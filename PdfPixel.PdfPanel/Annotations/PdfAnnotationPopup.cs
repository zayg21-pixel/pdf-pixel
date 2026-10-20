using PdfPixel.Annotations.Model;
using PdfPixel.Geometry;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Contains information about an annotation popup.
/// </summary>
public class PdfAnnotationPopup
{
    /// <summary>
    /// Initializes a popup with pre-computed hover rectangles and messages.
    /// Used when the document is not directly accessible — for example, on the WASM main thread
    /// after annotation data has been serialized from the worker.
    /// </summary>
    public PdfAnnotationPopup(bool isInteractive, PdfRectangle[] hoverRectangles, PdfAnnotationMessage[] messages)
    {
        IsInteractive = isInteractive;
        HoverRectangles = hoverRectangles ?? throw new ArgumentNullException(nameof(hoverRectangles));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    /// <summary>
    /// Initializes a popup backed by a live page annotation.
    /// Used when the document is loaded in-process (WPF path).
    /// </summary>
    internal PdfAnnotationPopup(PdfPageAnnotation pageAnnotation, PdfAnnotationMessage[] messages)
        : this(pageAnnotation.Content.IsInteractive, pageAnnotation.GetHoverRectangles(), messages)
    {
        PageAnnotation = pageAnnotation ?? throw new ArgumentNullException(nameof(pageAnnotation));
    }

    /// <summary>
    /// The page-bound annotation this popup represents.
    /// Available only when the document is loaded in-process. Null on the WASM main thread.
    /// </summary>
    public PdfPageAnnotation? PageAnnotation { get; }

    /// <summary>
    /// Whether this annotation accepts pointer interaction (hit-testing, cursor change, click).
    /// </summary>
    public bool IsInteractive { get; }

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
