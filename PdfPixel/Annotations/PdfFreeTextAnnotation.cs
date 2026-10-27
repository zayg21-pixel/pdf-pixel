using PdfPixel.Annotations.Parsing;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a free-form text annotation in a PDF document that allows users to add arbitrary text content.
/// </summary>
/// <remarks>Free-form text annotations do not display a bubble and require an appearance stream for rendering.
/// Fallback rendering is not implemented, as these annotations should always provide an appearance stream to ensure
/// correct display.</remarks>
public class PdfFreeTextAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFreeTextAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this free text annotation.</param>
    internal PdfFreeTextAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.FreeText)
    {
        PdfDictionary dictionary = annotationObject.Dictionary;

        DefaultAppearance = dictionary.GetString(PdfTokens.DefaultAppearanceKey);
        TextAlignment = (PdfTextAlignment)dictionary.GetIntegerOrDefault(PdfTokens.QuadKey);
        DefaultStyle = dictionary.GetString(PdfTokens.DSKey);

        PdfArray? calloutLineArray = dictionary.GetArray(PdfTokens.CLKey);
        if (calloutLineArray != null)
        {
            CalloutLine = PdfPoint.ParsePoints(calloutLineArray);
        }

        LineEnding = dictionary.GetNameOrDefault(PdfTokens.CalloutLineEndingKey).AsEnum<PdfLineEndingStyle>();
        BorderEffect = PdfAnnotationBorderParser.ParseBorderEffect(dictionary.GetDictionary(PdfTokens.BorderEffectKey));
        RectangleDifferences = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.RectDifferencesKey));
        ContentRectangle = ApplyRectDifferences(Rectangle, RectangleDifferences);

        // TODO: [MEDIUM] A FreeText without an appearance stream is not drawn:
        // - text in the default appearance, aligned by TextAlignment, inside ContentRectangle
        // - border of ContentRectangle with its border effect
        // - callout line and its line ending, not drawn for the typewriter intent
        // - default style string
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// Gets the default appearance string the text is formatted with (DA entry).
    /// </summary>
    public PdfString? DefaultAppearance { get; }

    /// <summary>
    /// Gets the justification of the text (Q entry). Default <see cref="PdfTextAlignment.Left"/>.
    /// </summary>
    public PdfTextAlignment TextAlignment { get; }

    /// <summary>
    /// Gets the default style string (DS entry).
    /// </summary>
    public PdfString? DefaultStyle { get; }

    /// <summary>
    /// Gets the starting, knee (when present) and ending points of the callout line (CL entry).
    /// </summary>
    public PdfPoint[]? CalloutLine { get; }

    /// <summary>
    /// Gets the line ending style at the starting point of <see cref="CalloutLine"/> (LE entry).
    /// </summary>
    public PdfLineEndingStyle LineEnding { get; }

    /// <summary>
    /// Gets the parsed border effect (BE entry).
    /// </summary>
    public PdfAnnotationBorderEffect BorderEffect { get; }

    /// <summary>
    /// Gets the rectangle differences that inset the text box from the annotation rectangle.
    /// </summary>
    public PdfRectangle? RectangleDifferences { get; }

    /// <summary>
    /// Gets the text box after applying <see cref="RectangleDifferences"/>.
    /// </summary>
    public PdfRectangle ContentRectangle { get; }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        // FreeText annotations should always have an appearance stream, so fallback rendering is not implemented.
        return false;
    }
}
