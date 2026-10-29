using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Forms;
using PdfPixel.Forms.Rendering;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a free-form text annotation in a PDF document that allows users to add arbitrary text content.
/// </summary>
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
        RectangleDifferences = PdfAnnotationGeometryParser.ParseInsets(dictionary.GetArray(PdfTokens.RectDifferencesKey));
        ContentRectangle = ApplyRectDifferences(Rectangle, RectangleDifferences);

        // TODO: [MEDIUM] A FreeText without an appearance stream does not apply:
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

    /// <inheritdoc/>
    public override PdfPoint ContentStart => new(ContentRectangle.Left, ContentRectangle.Bottom);

    /// <summary>
    /// Gets <see cref="ContentRectangle"/> grown by <paramref name="tolerance"/>, without the callout line.
    /// </summary>
    /// <param name="tolerance">Distance the hover area reaches beyond what is drawn.</param>
    internal override PdfPath GetHoverPath(float tolerance)
        => PdfAnnotationGeometry.CreateRectanglePath(ContentRectangle.Inflate(tolerance), borderStyle: null);

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfGraphicsState state = PdfAppearanceRenderer.CreateState(processor, page, renderer, DefaultAppearance, renderingParameters, observer);
        PdfColor color = state.FillPaint.Color;

        PdfColor backgroundColor = ResolveColor(page);
        if (backgroundColor != PdfColors.Transparent)
        {
            PdfAppearanceRenderer.DrawBackground(processor, ContentRectangle, BorderStyle, BorderEffect, backgroundColor);
        }

        PdfAppearanceRenderer.DrawBorder(processor, ContentRectangle, BorderStyle, BorderEffect, color);

        if (Intent == PdfAnnotationIntent.FreeTextCallout && CalloutLine != null)
        {
            PdfStrokeStyle strokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();
            DrawCalloutLine(processor, CalloutLine, strokeStyle, color, backgroundColor);
        }

        if (Contents != null && state.FontSize > 0)
        {
            PdfAppearanceRenderer.DrawText(processor, renderer, state, Contents.Value.DecodePdfString(), ContentRectangle, BorderStyle, TextAlignment);
        }

        return true;
    }

    private void DrawCalloutLine(
        IPdfCommandProcessor processor,
        PdfPoint[] calloutLine,
        PdfStrokeStyle strokeStyle,
        in PdfColor color,
        in PdfColor backgroundColor)
    {
        PdfPath? calloutPath = PdfAnnotationGeometry.CreatePolylinePath(calloutLine);
        if (calloutPath == null)
        {
            return;
        }

        processor.Process(new DrawPathCommand(calloutPath, PdfAnnotationPaintFactory.CreateStrokePaint(color, strokeStyle)));

        PdfPoint start = calloutLine[0];
        PdfPoint afterStart = calloutLine[1];
        PdfAnnotationLineEndingRenderer.DrawLineEnding(
            processor,
            start.X,
            start.Y,
            afterStart.X,
            afterStart.Y,
            LineEnding,
            strokeStyle.LineWidth,
            color,
            backgroundColor);
    }
}
