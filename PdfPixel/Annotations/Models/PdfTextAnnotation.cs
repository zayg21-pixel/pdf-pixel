using PdfPixel.Annotations.Rendering;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF text annotation.
/// </summary>
/// <remarks>
/// Text annotations represent "sticky notes" attached to a point in the PDF document.
/// When closed, they appear as an icon; when open, they display a pop-up window
/// containing the text of the note in a font and size chosen by the conforming reader.
/// </remarks>
public class PdfTextAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTextAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this text annotation.</param>
    public PdfTextAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Text)
    {
        // Initialize all text annotation specific properties
        IsOpen = annotationObject.Dictionary.GetBooleanOrDefault(PdfTokens.OpenKey);
        Icon = annotationObject.Dictionary.GetName(PdfTokens.NameKey).AsEnum<PdfTextAnnotationIcon>();
        StateModel = annotationObject.Dictionary.GetName(PdfTokens.StateModelKey);
        State = annotationObject.Dictionary.GetName(PdfTokens.StateKey);

        SKRect rectangle = base.Rectangle;
        Rectangle = (rectangle.Width > 0 && rectangle.Height > 0)
            ? rectangle
            : SKRect.Create(rectangle.Left, rectangle.Top, PdfAnnotationGraphics.DefaultBubbleSize, PdfAnnotationGraphics.DefaultBubbleSize);
    }

    /// <inheritdoc/>
    public override SKRect Rectangle { get; }

    /// <summary>
    /// Gets a value indicating whether the annotation is initially displayed in an open state.
    /// </summary>
    /// <remarks>
    /// Default value is false (closed).
    /// </remarks>
    public bool IsOpen { get; }

    /// <summary>
    /// Gets the name of an icon to be displayed when the annotation is closed.
    /// </summary>
    public PdfTextAnnotationIcon Icon { get; }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <inheritdoc/>
    public override bool IsInteractive => true;

    /// <inheritdoc/>
    public override SKRect GetHoverRectangle(IPdfPage page) => Rectangle;

    /// <summary>
    /// Gets the state model corresponding to a change in the annotation's state.
    /// </summary>
    /// <remarks>
    /// Common state models include "Review" and "Marked".
    /// </remarks>
    public PdfString StateModel { get; }

    /// <summary>
    /// Gets the state value corresponding to the state model.
    /// </summary>
    /// <remarks>
    /// For "Review" state model: None, Accepted, Rejected, Cancelled, Completed, etc.
    /// For "Marked" state model: Marked, Unmarked.
    /// </remarks>
    public PdfString State { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        PdfAnnotationIconDefinition? iconDefinition =
            PdfAnnotationGraphics.GetAnnotationIcon(Icon.ToString(), visualStateKind)
                ?? PdfAnnotationGraphics.GetAnnotationBubbleIcon(visualStateKind);

        if (iconDefinition == null)
        {
            return false;
        }

        SKColor borderColor = ResolveColor(page, PdfAnnotationGraphics.DefaultBubbleBorderColor);
        SKColor backgroundColor = ResolveInteriorColor(page, PdfAnnotationGraphics.DefaultBubbleBackgroundColor);
        PdfAnnotationGraphics.RenderIcon(processor, iconDefinition, GetHoverRectangle(page), borderColor, backgroundColor);
        return true;
    }

    /// <summary>
    /// Returns a string representation of this text annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and contents.</returns>
    public override string ToString()
    {
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Text Annotation ({Icon}): {contentsText}";
        }

        return $"Text Annotation ({Icon})";
    }
}
