using System;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Contains information about a single annotation message and the messages that reply to it.
/// </summary>
public sealed class PdfAnnotationMessage
{
    /// <summary>
    /// Initializes a message with the given date, title, body text, and replies.
    /// </summary>
    public PdfAnnotationMessage(DateTimeOffset? date, string? title, string contents, PdfAnnotationMessage[] replies)
    {
        CreationDate = date;
        Title = title;
        Contents = contents;
        Replies = replies ?? throw new ArgumentNullException(nameof(replies));
    }

    /// <summary>
    /// Date when the message was created.
    /// </summary>
    public DateTimeOffset? CreationDate { get; }

    /// <summary>
    /// Title of the message.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Contents of the message.
    /// </summary>
    public string Contents { get; }

    /// <summary>
    /// Messages replying to this one, in document order.
    /// </summary>
    public PdfAnnotationMessage[] Replies { get; }
}
