using PdfPixel.Annotations.Models;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Contains information about an annotation popup and it's location.
/// </summary>
public class PdfAnnotationPopup
{
    public PdfAnnotationPopup(PdfPanelAnnotationAction action, PdfAnnotationMessage[] messages, SKRect rect, bool isInteractive)
    {
        Action = action;
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        Rect = rect;
        IsInteractive = isInteractive;
    }

    /// <summary>
    /// Attached base annotation.
    /// </summary>
    public PdfAnnotationBase Annotation { get; set; }

    /// <summary>
    /// Resolved, document-independent action for this annotation, or null if the annotation has no action.
    /// </summary>
    public PdfPanelAnnotationAction Action { get; }

    /// <summary>
    /// Thread of annotation messages (from oldest to newest).
    /// First message is the root annotation, subsequent messages are replies in chronological order.
    /// </summary>
    public PdfAnnotationMessage[] Messages { get; }

    /// <summary>
    /// Position of the popup rectangle in PDF coordinates.
    /// </summary>
    public SKRect Rect { get; }

    /// <summary>
    /// Determines if this annotation popup is interactive (e.g., link, bubble, or has special visual states).
    /// </summary>
    /// <returns>True if the annotation is interactive; otherwise, false.</returns>
    public bool IsInteractive { get; }

    public override int GetHashCode()
    {
        return Rect.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is PdfAnnotationPopup other && 
               Equals(Messages, other.Messages) && 
               Rect.Equals(other.Rect);
    }
}
