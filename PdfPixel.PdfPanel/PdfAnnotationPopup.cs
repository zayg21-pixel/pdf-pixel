using PdfPixel.Annotations.Models;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Contains information about an annotation popup and it's location.
/// </summary>
public class PdfAnnotationPopup
{
    public PdfAnnotationPopup(PdfAnnotationBase annotation, AnnotationMessage[] messages, SKRect rect)
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
    public AnnotationMessage[] Messages { get; }

    /// <summary>
    /// Position of the popup rectangle in PDF coordinates.
    /// </summary>
    public SKRect Rect { get; }

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

/// <summary>
/// Contains information about a single annotation message.
/// </summary>
public readonly struct AnnotationMessage
{
    public AnnotationMessage(DateTimeOffset? date, string title, string contents)
    {
        CreationDate = date;
        Title = title;
        Contents = contents;
    }

    /// <summary>
    /// Date when the message was created.
    /// </summary>
    public DateTimeOffset? CreationDate { get; }

    /// <summary>
    /// Title of the message.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Contents of the message.
    /// </summary>
    public string Contents { get; }
}