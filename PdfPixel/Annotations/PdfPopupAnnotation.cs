using PdfPixel.Commands;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF popup annotation.
/// </summary>
public class PdfPopupAnnotation : PdfAnnotation
{
    /// <summary>
    /// Gets whether this annotation carries text to show in a pop-up window.
    /// </summary>
    public override bool HasPopupContent => false;

    /// <summary>
    /// Gets whether this annotation should display a content bubble indicator.
    /// </summary>
    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPopupAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this popup annotation.</param>
    internal PdfPopupAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Popup)
    {
        Parent = annotationObject.Dictionary.GetReference(PdfTokens.ParentKey);
        IsOpen = annotationObject.Dictionary.GetBooleanOrDefault(PdfTokens.OpenKey);
    }

    /// <summary>
    /// Gets the reference to the parent annotation that this popup is associated with.
    /// </summary>
    public PdfReference? Parent { get; }

    /// <summary>
    /// Gets a value indicating whether the annotation is initially displayed in an open state.
    /// </summary>
    public bool IsOpen { get; }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        return false;
    }

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
