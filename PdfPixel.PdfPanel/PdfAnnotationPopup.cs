using PdfPixel.Annotations.Models;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Contains information about an annotation popup and it's location.
/// </summary>
public class PdfAnnotationPopup
{
    public PdfAnnotationPopup(PdfAnnotationBase annotation, PdfAnnotationMessage[] messages, SKRect rect)
    {
        Annotation = annotation ?? throw new ArgumentNullException(nameof(annotation));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        Rect = rect;
    }

    /// <summary>
    /// Reference to the original PDF annotation.
    /// </summary>
    public PdfAnnotationBase Annotation { get; }

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
    public bool IsInteractive()
    {
        if (Annotation is PdfLinkAnnotation || Annotation is PdfFileAttachmentAnnotation)
        {
            return true;
        }

        if (Annotation.ShouldDisplayBubble)
        {
            return true;
        }
        if (Annotation.SupportedVisualStates != PdfAnnotationVisualStateKind.Normal &&
            Annotation.SupportedVisualStates != PdfAnnotationVisualStateKind.None)
        {
            return true;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Annotation, Rect);
    }

    public override bool Equals(object obj)
    {
        return obj is PdfAnnotationPopup other && 
               Equals(Annotation, other.Annotation) && 
               Rect.Equals(other.Rect);
    }
}
