using System;

namespace PdfPixel.PdfPanel.Annotations;

// TODO: rework into a hierarchy. Replies form a tree through /IRT, and flattening a thread into an
// ordered array loses which message each reply answers. The type needs to carry its own replies so the
// tree survives extraction, and PdfDocumentAnnotationExtractor must then walk it without dropping
// subtrees under a non-/R node or discarding replies whose parent is not on the page.

/// <summary>
/// Contains information about a single annotation message.
/// </summary>
public readonly struct PdfAnnotationMessage
{
    /// <summary>
    /// Initializes a message with the given date, title, and body text.
    /// </summary>
    public PdfAnnotationMessage(DateTimeOffset? date, string? title, string contents)
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
    public string? Title { get; }

    /// <summary>
    /// Contents of the message.
    /// </summary>
    public string Contents { get; }
}

