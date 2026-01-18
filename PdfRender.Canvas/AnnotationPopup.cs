using SkiaSharp;
using System;
using System.Linq;

namespace PdfRender.Canvas;

/// <summary>
/// Contains information about an annotation popup and it's location.
/// </summary>
public class AnnotationPopup
{
    public AnnotationPopup(AnnotationMessage[] messages, SKRect rect)
    {
        Messages = messages;
        Rect = rect;
    }

    /// <summary>
    /// Collection of popup messages.
    /// </summary>
    public AnnotationMessage[] Messages { get; }

    /// <summary>
    /// Position of the popup rectangle in PDF coordinates.
    /// </summary>
    public SKRect Rect { get; }

    public override int GetHashCode()
    {
        var code = 0;

        foreach (var message in Messages)
        {
            code ^= message.GetHashCode();
        }

        return code;
    }

    public override bool Equals(object obj)
    {
        if (obj is AnnotationPopup other)
        {
            return Messages.SequenceEqual(other.Messages);
        }
        else
        {
            return false;
        }
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