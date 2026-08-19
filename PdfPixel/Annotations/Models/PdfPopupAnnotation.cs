using PdfPixel.Commands.Model;
using PdfPixel.Models;
using PdfPixel.Text;

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
    /// Gets whether this annotation carries text to show in a pop-up window.
    /// </summary>
    /// <remarks>
    /// A popup annotation is the window itself, shown on behalf of its parent, so it is never
    /// a trigger of its own.
    /// </remarks>
    public override bool HasPopupContent => false;

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
        ParentAnnotation = annotationObject.Dictionary.GetReference(PdfTokens.ParentKey);
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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind) => false;

    /// <summary>
    /// Returns a string representation of this popup annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and open state.</returns>
    public override string ToString()
    {
        string state = IsOpen ? "Open" : "Closed";

        if (Contents != null)
        {
            return $"Popup Annotation ({state}): {Contents}";
        }

        return $"Popup Annotation ({state})";
    }
}
