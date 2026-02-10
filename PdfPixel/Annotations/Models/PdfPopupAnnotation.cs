using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF popup annotation.
/// </summary>
/// <remarks>
/// Popup annotations display a pop-up window containing text of another annotation.
/// They are typically associated with text annotations and other markup annotations
/// to display additional content in a separate window. The popup annotation itself
/// is invisible and only defines where a popup window would appear. Rendering of
/// popup content is handled by the viewer application.
/// </remarks>
public class PdfPopupAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Gets whether this annotation should display a content bubble indicator.
    /// </summary>
    /// <remarks>
    /// Popup annotations are non-visual containers that define the position of a pop-up window
    /// for another annotation. They should never render an additional bubble indicator.
    /// </remarks>
    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPopupAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this popup annotation.</param>
    public PdfPopupAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Popup)
    {
        ParentAnnotation = annotationObject.Dictionary.GetObject(PdfTokens.ParentKey)?.Reference;
        IsOpen = annotationObject.Dictionary.GetBooleanOrDefault(PdfTokens.OpenKey);
    }

    /// <summary>
    /// Gets the reference to the parent annotation that this popup is associated with.
    /// </summary>
    public PdfReference? ParentAnnotation { get; }

    /// <summary>
    /// Gets a value indicating whether the annotation is initially displayed in an open state.
    /// </summary>
    public bool IsOpen { get; }

    /// <summary>
    /// Creates a fallback rendering for popup annotations.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>Null - popup annotations are invisible and don't render visual content.</returns>
    /// <remarks>
    /// Popup annotations are metadata that define where a popup window would appear.
    /// They don't have a visual representation themselves. Most PDF viewers either ignore
    /// them entirely or implement their own popup UI style.
    /// </remarks>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        return null;
    }

    /// <summary>
    /// Returns a string representation of this popup annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and open state.</returns>
    public override string ToString()
    {
        var state = IsOpen ? "Open" : "Closed";
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Popup Annotation ({state}): {contentsText}";
        }

        return $"Popup Annotation ({state})";
    }
}
