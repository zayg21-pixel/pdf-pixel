using PdfPixel.Annotations.Models;
using System;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Contains information about an annotation popup.
/// </summary>
public class PdfAnnotationPopup
{
    /// <summary>
    /// Initializes a popup with the given page annotation and messages.
    /// </summary>
    public PdfAnnotationPopup(PdfPageAnnotation pageAnnotation, PdfAnnotationMessage[] messages)
    {
        PageAnnotation = pageAnnotation ?? throw new ArgumentNullException(nameof(pageAnnotation));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    /// <summary>
    /// The page-bound annotation this popup represents.
    /// </summary>
    public PdfPageAnnotation PageAnnotation { get; }

    /// <summary>
    /// Thread of annotation messages (from oldest to newest).
    /// First message is the root annotation, subsequent messages are replies in chronological order.
    /// </summary>
    public PdfAnnotationMessage[] Messages { get; }

    /// <inheritdoc />
    public override int GetHashCode() => PageAnnotation.GetHashCode();

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is PdfAnnotationPopup other
            && ReferenceEquals(PageAnnotation, other.PageAnnotation)
            && Equals(Messages, other.Messages);
    }
}
