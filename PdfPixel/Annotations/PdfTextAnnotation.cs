using PdfPixel.Annotations.Icons;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF text annotation.
/// </summary>
public class PdfTextAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTextAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this text annotation.</param>
    internal PdfTextAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Text)
    {
        // Initialize all text annotation specific properties
        IsOpen = annotationObject.Dictionary.GetBooleanOrDefault(PdfTokens.OpenKey);
        Icon = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.NameKey).AsEnum<PdfTextAnnotationIcon>();
        StateModel = annotationObject.Dictionary.GetName(PdfTokens.StateModelKey);
        State = annotationObject.Dictionary.GetName(PdfTokens.StateKey);

        PdfRectangle rectangle = base.Rectangle;
        Rectangle = (rectangle.Width > 0 && rectangle.Height > 0)
            ? rectangle
            : PdfRectangle.FromLocationAndSize(rectangle.Left, rectangle.Top, PdfAnnotationGraphics.DefaultBubbleSize, PdfAnnotationGraphics.DefaultBubbleSize);
    }

    /// <inheritdoc/>
    public override PdfRectangle Rectangle { get; }

    /// <summary>
    /// Gets a value indicating whether the annotation is initially displayed in an open state.
    /// </summary>
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
    public override PdfRectangle[] GetHoverRectangles(IPdfPage page) => [Rectangle];

    /// <summary>
    /// Gets the state model corresponding to a change in the annotation's state.
    /// </summary>
    public PdfString? StateModel { get; }

    /// <summary>
    /// Gets the state value corresponding to the state model.
    /// </summary>
    public PdfString? State { get; }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfAnnotationIconDefinition? iconDefinition =
            PdfAnnotationGraphics.GetAnnotationIcon(Icon.ToString(), visualStateKind)
                ?? PdfAnnotationGraphics.GetAnnotationBubbleIcon(visualStateKind);

        if (iconDefinition == null)
        {
            return false;
        }

        PdfColor borderColor = ResolveColor(page, PdfAnnotationGraphics.DefaultBubbleBorderColor);
        PdfColor backgroundColor = ResolveInteriorColor(page, PdfAnnotationGraphics.DefaultBubbleBackgroundColor);
        PdfAnnotationGraphics.RenderIcon(processor, iconDefinition, Rectangle, borderColor, backgroundColor);
        return true;
    }

    /// <summary>
    /// Returns a string representation of this text annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and contents.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Text Annotation ({Icon}): {Contents}";
        }

        return $"Text Annotation ({Icon})";
    }
}
