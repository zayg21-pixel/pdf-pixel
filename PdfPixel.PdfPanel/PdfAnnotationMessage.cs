using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Contains information about a single annotation message.
/// </summary>
public readonly struct PdfAnnotationMessage
{
    public PdfAnnotationMessage(DateTimeOffset? date, string title, string contents)
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