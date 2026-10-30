using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Forms;
using PdfPixel.Forms.Rendering;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Resources;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF redaction annotation, which marks content that is intended to be removed. The marked region is
/// shown with its overlay; applying the redaction is not supported.
/// </summary>
public class PdfRedactAnnotation : PdfMarkupAnnotation
{
    private const float OverlayTextPadding = 2f;

    private readonly PdfPath _regionPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRedactAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this redaction annotation.</param>
    internal PdfRedactAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Redact)
    {
        PdfDictionary dictionary = annotationObject.Dictionary;

        Quadrilaterals = PdfAnnotationGeometryParser.ParseQuadrilaterals(dictionary.GetArray(PdfTokens.QuadPointsKey));
        OverlayAppearance = PdfXObject.GetXObject(dictionary, PdfTokens.ROKey) as PdfForm;
        OverlayText = dictionary.GetString(PdfTokens.OverlayTextKey);
        Repeat = dictionary.GetBooleanOrDefault(PdfTokens.RepeatKey);
        DefaultAppearance = dictionary.GetString(PdfTokens.DefaultAppearanceKey);
        TextAlignment = (PdfTextAlignment)dictionary.GetIntegerOrDefault(PdfTokens.QuadKey);

        PdfPathBuilder regionPath = new();

        if (Quadrilaterals.Length > 0)
        {
            regionPath.AddQuadrilaterals(Quadrilaterals);
        }
        else
        {
            regionPath.AddRect(Rectangle);
        }

        _regionPath = regionPath.ToPath();
    }

    /// <summary>
    /// Gets the quadrilaterals of the content intended to be removed (QuadPoints entry), each as four points. Empty when
    /// <see cref="PdfAnnotation.Rectangle"/> is the region.
    /// </summary>
    public PdfPoint[][] Quadrilaterals { get; }

    /// <summary>
    /// Gets the overlay appearance drawn over the region once it is redacted (RO entry), or null when absent. It takes
    /// precedence over <see cref="PdfAnnotation.InteriorColor"/>, <see cref="OverlayText"/>,
    /// <see cref="DefaultAppearance"/> and <see cref="TextAlignment"/>.
    /// </summary>
    public PdfForm? OverlayAppearance { get; }

    /// <summary>
    /// Gets the text drawn over the region once it is redacted (OverlayText entry), or null when absent.
    /// </summary>
    public PdfString? OverlayText { get; }

    /// <summary>
    /// Gets whether <see cref="OverlayText"/> is repeated to fill the region (Repeat entry). Default false.
    /// </summary>
    public bool Repeat { get; }

    /// <summary>
    /// Gets the default appearance string <see cref="OverlayText"/> is formatted with (DA entry).
    /// </summary>
    public PdfString? DefaultAppearance { get; }

    /// <summary>
    /// Gets the justification of <see cref="OverlayText"/> (Q entry). Default <see cref="PdfTextAlignment.Left"/>.
    /// </summary>
    public PdfTextAlignment TextAlignment { get; }

    /// <inheritdoc/>
    public override bool IsInteractive => true;

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <inheritdoc/>
    protected override bool UsesOpacityLayer => false;

    /// <summary>
    /// Gets the top-left point of the first quadrilateral, or the upper-left corner of the rectangle when there are none.
    /// </summary>
    public override PdfPoint ContentStart => (Quadrilaterals.Length > 0) ? Quadrilaterals[0][2] : base.ContentStart;

    /// <summary>
    /// Gets the region grown by <paramref name="tolerance"/>.
    /// </summary>
    /// <param name="tolerance">Distance the hover area reaches beyond what is drawn.</param>
    internal override PdfPath GetHoverPath(float tolerance) => _regionPath.Inflate(tolerance);

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        if (visualStateKind != PdfAnnotationVisualStateKind.Normal)
        {
            RenderOverlay(processor, page, renderer, renderingParameters, observer);
        }

        return true;
    }

    /// <summary>
    /// Draws what the region shows once it is redacted: <see cref="OverlayAppearance"/> with its origin at the
    /// lower-left corner of the rectangle, otherwise the region filled with the interior color, white when absent, and
    /// the overlay text.
    /// </summary>
    private void RenderOverlay(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        if (OverlayAppearance != null)
        {
            PdfPoint origin = new(Rectangle.Left, Rectangle.Top);
            PdfAnnotationAppearanceRenderer.RenderFormAt(processor, OverlayAppearance, origin, page, renderer, renderingParameters, observer);
        }
        else
        {
            RenderInteriorOverlay(processor, page, renderer, renderingParameters, observer);
        }
    }

    private void RenderInteriorOverlay(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfColor interiorColor = ResolveInteriorColor(page, PdfColors.White);

        if (interiorColor != PdfColors.Transparent)
        {
            processor.Process(new DrawPathCommand(_regionPath, PdfAnnotationPaintFactory.CreateFillPaint(interiorColor)));
        }

        if (OverlayText == null)
        {
            return;
        }

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ClipPathCommand(_regionPath, PdfClipOperation.Intersect));

        PdfGraphicsState state = PdfAppearanceRenderer.CreateState(processor, page, renderer, DefaultAppearance, renderingParameters, observer);
        string text = OverlayText.Value.DecodePdfString();

        if (Quadrilaterals.Length > 0)
        {
            foreach (PdfPoint[] quadrilateral in Quadrilaterals)
            {
                PdfRectangle? quadrilateralBounds = PdfRectangle.FromPoints(quadrilateral);

                if (quadrilateralBounds != null)
                {
                    DrawOverlayText(processor, renderer, state, text, quadrilateralBounds.Value);
                }
            }
        }
        else
        {
            DrawOverlayText(processor, renderer, state, text, Rectangle);
        }

        processor.Process(RestoreStateCommand.Instance);
    }

    private void DrawOverlayText(IPdfCommandProcessor processor, PdfRenderer renderer, PdfGraphicsState state, string text, in PdfRectangle box)
    {
        PdfRectangle textBox = box.Inflate(-OverlayTextPadding);

        if (Repeat)
        {
            DrawRepeatedOverlayText(processor, renderer, state, text, textBox);
        }
        else
        {
            PdfAppearanceRenderer.DrawText(processor, renderer, state, text, textBox, TextAlignment);
        }
    }

    /// <summary>
    /// Fills <paramref name="textBox"/> with the text as laid out once, repeated edge to edge in steps of the area it
    /// takes.
    /// </summary>
    private void DrawRepeatedOverlayText(
        IPdfCommandProcessor processor,
        PdfRenderer renderer,
        PdfGraphicsState state,
        string text,
        in PdfRectangle textBox)
    {
        PdfCommandRecorder cellRecorder = new();
        PdfRectangle cellBox = PdfAppearanceRenderer.DrawText(cellRecorder, renderer, state, text, textBox, TextAlignment);

        if (cellBox.Width <= 0f || cellBox.Height <= 0f)
        {
            return;
        }

        DrawRecordingCommand recordingCommand = new(cellRecorder);

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ClipRectangleCommand(textBox, PdfClipOperation.Intersect));
        processor.Process(new DrawTilingCommand(PdfMatrix.Identity, textBox, cellBox, cellBox.Width, cellBox.Height, recordingCommand));
        processor.Process(RestoreStateCommand.Instance);
    }

    /// <summary>
    /// Returns a string representation of this redaction annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Redact Annotation: {Contents}";
        }

        return "Redact Annotation";
    }
}
