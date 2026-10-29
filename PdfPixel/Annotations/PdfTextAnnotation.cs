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

        float iconSize = PdfAnnotationGraphics.DefaultBubbleSize;
        ContentBounds = (Rectangle.Width > 0 && Rectangle.Height > 0)
            ? Rectangle
            : PdfRectangle.FromLocationAndSize(Rectangle.Left, Rectangle.Bottom - iconSize, iconSize, iconSize);
    }

    /// <summary>
    /// Gets the rectangle the icon is drawn in: <see cref="PdfAnnotation.Rectangle"/>, or an icon of the default size
    /// with its top-left corner at the upper-left corner of <see cref="PdfAnnotation.Rectangle"/> when it has no area.
    /// </summary>
    public override PdfRectangle ContentBounds { get; }

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

    /// <summary>
    /// Gets the icon rectangle grown by <paramref name="tolerance"/>.
    /// </summary>
    /// <param name="tolerance">Distance the hover area reaches beyond what is drawn.</param>
    internal override PdfPath GetHoverPath(float tolerance)
        => PdfAnnotationGeometry.CreateRectanglePath(ContentBounds.Inflate(tolerance), borderStyle: null);

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

        PdfAnnotationGraphics.RenderIcon(processor, iconDefinition, ContentBounds, PdfColors.Black, ResolveColor(page));
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
