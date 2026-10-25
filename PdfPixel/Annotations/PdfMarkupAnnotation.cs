using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Text;
using System;

namespace PdfPixel.Annotations;

/// <summary>
/// Base class for markup annotations, the annotations that carry an author, a creation date, a pop-up
/// window and replies in addition to the entries common to all annotations.
/// </summary>
public abstract class PdfMarkupAnnotation : PdfAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfMarkupAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this markup annotation.</param>
    /// <param name="subtype">The markup annotation subtype.</param>
    private protected PdfMarkupAnnotation(PdfObject annotationObject, PdfAnnotationSubType subtype)
        : base(annotationObject, subtype)
    {
        if (annotationObject == null)
        {
            throw new ArgumentNullException(nameof(annotationObject));
        }

        RichContents = annotationObject.Dictionary.GetString(PdfTokens.RichContentsKey);

        if (Contents == null)
        {
            Contents = PdfRichTextContentParser.ExtractPlainText(RichContents);
        }

        Title = annotationObject.Dictionary.GetString(PdfTokens.TitleKey);
        Subject = annotationObject.Dictionary.GetString(PdfTokens.SubjectKey);
        CreationDate = PdfDateParser.ParsePdfDate(annotationObject.Dictionary.GetString(PdfTokens.CreationDateKey));
        Popup = annotationObject.Dictionary.GetReference(PdfTokens.PopupKey);
        InReplyTo = annotationObject.Dictionary.GetReference(PdfTokens.InReplyToKey);
        ReplyType = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.ReplyTypeKey).AsEnum<PdfAnnotationReplyType>();
        Intent = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.ITKey).AsEnum<PdfAnnotationIntent>();
    }

    /// <inheritdoc />
    public override bool HasPopupContent => base.HasPopupContent || RichContents != null;

    /// <inheritdoc />
    /// <remarks>
    /// An annotation that carries no <see cref="Popup"/> triggers its pop-up from its own area
    /// instead, so it shows no separate bubble.
    /// </remarks>
    public override bool ShouldDisplayBubble => Popup != null && HasPopupContent;

    /// <summary>
    /// Gets the annotation's rich text contents (the RC entry), an XHTML-subset
    /// markup representation of <see cref="PdfAnnotation.Contents"/>.
    /// </summary>
    public PdfString? RichContents { get; }

    /// <summary>
    /// Gets the annotation title/author.
    /// </summary>
    /// <remarks>
    /// The title is typically used to identify the author or creator of the annotation.
    /// </remarks>
    public PdfString? Title { get; }

    /// <summary>
    /// Gets the annotation subject.
    /// </summary>
    /// <remarks>
    /// The subject represents a short description of the subject being addressed by the annotation.
    /// </remarks>
    public PdfString? Subject { get; }

    /// <summary>
    /// Gets the creation date of the annotation.
    /// </summary>
    /// <remarks>
    /// The creation date represents when the annotation was first created.
    /// Returns null if the date is not present or could not be parsed.
    /// </remarks>
    public DateTime? CreationDate { get; }

    /// <summary>
    /// Gets the reference to the popup annotation associated with this markup annotation, if any.
    /// </summary>
    public PdfReference? Popup { get; }

    /// <summary>
    /// Gets the reference to the annotation that this annotation is in reply to.
    /// </summary>
    /// <remarks>
    /// Used to create threaded discussions where annotations can reply to other annotations.
    /// Returns null if this annotation is not a reply.
    /// </remarks>
    public PdfReference? InReplyTo { get; }

    /// <summary>
    /// Gets the reply type indicating the relationship between this annotation and the one specified by InReplyTo.
    /// </summary>
    /// <remarks>
    /// Reply (R) creates a linear thread, Group allows multiple replies to same parent.
    /// Returns None if not specified or if this annotation is not a reply.
    /// </remarks>
    public PdfAnnotationReplyType ReplyType { get; }

    /// <summary>
    /// Intent of the markup annotation (/IT). Default <see cref="PdfAnnotationIntent.None"/>.
    /// </summary>
    public PdfAnnotationIntent Intent { get; }
}
