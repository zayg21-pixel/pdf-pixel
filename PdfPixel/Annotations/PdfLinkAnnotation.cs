using PdfPixel.Actions;
using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Navigation;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF link annotation.
/// </summary>
public class PdfLinkAnnotation : PdfAnnotation
{
    private const float HighlightLineWidth = 1f;

    private readonly PdfPath _activePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLinkAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this link annotation.</param>
    internal PdfLinkAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Link)
    {
        Destination = annotationObject.Dictionary.Document.Destinations.Parse(annotationObject.Dictionary, PdfTokens.DestKey);

        Action = PdfAction.GetAction(annotationObject.Dictionary, PdfTokens.AKey);

        HighlightMode = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.HighlightModeKey).AsEnum<PdfLinkHighlightMode>();
        PdfPoint[][] quadPoints = PdfAnnotationGeometryParser.ParseQuadrilaterals(annotationObject.Dictionary.GetArray(PdfTokens.QuadPointsKey));

        if (AreWithinRectangle(Rectangle, quadPoints))
        {
            PdfPathBuilder activePath = new();
            activePath.AddQuadrilaterals(quadPoints);

            Quadrilaterals = quadPoints;
            _activePath = activePath.ToPath();
        }
        else
        {
            PdfPoint[] rectangleCorners =
            [
                new(Rectangle.Right, Rectangle.Top),
                new(Rectangle.Left, Rectangle.Top),
                new(Rectangle.Left, Rectangle.Bottom),
                new(Rectangle.Right, Rectangle.Bottom)
            ];

            Quadrilaterals = new PdfPoint[][] { rectangleCorners };
            _activePath = PdfAnnotationGeometry.CreateRectanglePath(Rectangle, BorderStyle);
        }
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <inheritdoc/>
    public override PdfPoint ContentStart => Quadrilaterals[0][2];

    /// <inheritdoc/>
    public override bool IsInteractive => true;

    /// <summary>
    /// Gets the quadrilaterals the link is active in, each as four points, e.g. one per line of a link
    /// spanning two lines of text. A single quadrilateral of the rectangle when the link has none, or when any of
    /// them lies outside <see cref="PdfAnnotation.Rectangle"/>.
    /// </summary>
    public PdfPoint[][] Quadrilaterals { get; }

    /// <summary>
    /// Gets the destination (/Dest) shown when the annotation is activated, resolved by this document's
    /// <see cref="PdfDestinationResolver"/>. Null if the link uses an Action instead; per PDF spec a link
    /// carries either a Dest or an A entry.
    /// </summary>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Gets the action to be performed when the annotation is activated, or null when absent.
    /// </summary>
    public PdfAction? Action { get; }

    /// <summary>
    /// Gets the highlight mode that specifies the visual effect to use when the link is activated.
    /// </summary>
    public PdfLinkHighlightMode HighlightMode { get; }

    /// <summary>
    /// Gets the active area, <see cref="Quadrilaterals"/> when every quadrilateral lies within
    /// <see cref="PdfAnnotation.Rectangle"/>, otherwise the rectangle, grown by <paramref name="tolerance"/>.
    /// </summary>
    /// <param name="tolerance">Distance the hover area reaches beyond what is drawn.</param>
    internal override PdfPath GetHoverPath(float tolerance) => _activePath.Inflate(tolerance);

    internal override bool RenderAppearanceStream(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        bool rendered = base.RenderAppearanceStream(processor, page, visualStateKind, renderer, renderingParameters, observer);

        if (rendered
            && visualStateKind == PdfAnnotationVisualStateKind.Down
            && Appearance != null
            && (Appearance.SupportedStates & PdfAnnotationVisualStateKind.Down) == 0)
        {
            RenderHighlight(processor, BorderStyle?.StrokeStyle ?? new PdfStrokeStyle());
        }

        return rendered;
    }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfStrokeStyle strokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();

        if (strokeStyle.LineWidth > 0 && Color?.Length != 0)
        {
            PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(ResolveColor(page), GetInsideStrokeStyle(strokeStyle));
            DrawBorder(processor, paint);
        }

        if (visualStateKind == PdfAnnotationVisualStateKind.Down)
        {
            RenderHighlight(processor, strokeStyle);
        }

        return true;
    }

    /// <summary>
    /// Strokes the border inside the active area with <paramref name="paint"/>, whose line is twice the border width:
    /// the outline of the area, or only the bottom edge of every quadrilateral for the underline border style,
    /// clipped to the area.
    /// </summary>
    private void DrawBorder(IPdfCommandProcessor processor, PdfPaint paint)
    {
        PdfPath borderPath = _activePath;

        if (BorderStyle?.StyleType == PdfAnnotationBorderStyleType.Underline)
        {
            PdfPathBuilder underlinePath = new();

            foreach (PdfPoint[] quadrilateral in Quadrilaterals)
            {
                underlinePath.MoveTo(quadrilateral[0]);
                underlinePath.LineTo(quadrilateral[1]);
            }

            borderPath = underlinePath.ToPath();
        }

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ClipPathCommand(_activePath, PdfClipOperation.Intersect));
        processor.Process(new DrawPathCommand(borderPath, paint));
        processor.Process(RestoreStateCommand.Instance);
    }

    /// <summary>
    /// Returns <paramref name="strokeStyle"/> with twice its line width, so that the half left inside the clip of
    /// <see cref="DrawBorder"/> is the border width.
    /// </summary>
    private static PdfStrokeStyle GetInsideStrokeStyle(PdfStrokeStyle strokeStyle) => strokeStyle.WithLineWidth(strokeStyle.LineWidth * 2f);

    private void RenderHighlight(IPdfCommandProcessor processor, PdfStrokeStyle strokeStyle)
    {
        switch (HighlightMode)
        {
            case PdfLinkHighlightMode.Invert:
            {
                processor.Process(new DrawPathCommand(_activePath, PdfAnnotationPaintFactory.CreateInvertFillPaint()));
                break;
            }
            case PdfLinkHighlightMode.Outline:
            {
                PdfStrokeStyle outlineStyle = (strokeStyle.LineWidth > 0) ? strokeStyle : new PdfStrokeStyle();
                DrawBorder(processor, PdfAnnotationPaintFactory.CreateInvertStrokePaint(GetInsideStrokeStyle(outlineStyle)));
                break;
            }
            case PdfLinkHighlightMode.Push:
            {
                Push(processor, strokeStyle.LineWidth);
                break;
            }
        }
    }

    /// <summary>
    /// Draws lines along the left and top inside every active quadrilateral, inset by <paramref name="borderWidth"/>
    /// plus half the line, ending <paramref name="borderWidth"/> short of the bottom and right edges.
    /// </summary>
    private void Push(IPdfCommandProcessor processor, float borderWidth)
    {
        PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(PdfColors.Black, HighlightLineWidth);
        float lineInset = borderWidth + (HighlightLineWidth / 2);
        PdfPathBuilder pushPath = new();

        foreach (PdfPoint[] quadrilateral in Quadrilaterals)
        {
            PdfAnnotationGeometry.AddInsetLeftTopEdges(pushPath, quadrilateral, lineInset, borderWidth);
        }

        processor.Process(new DrawPathCommand(pushPath.ToPath(), paint));
    }

    /// <summary>
    /// Whether <paramref name="quadrilaterals"/> define the active area: there is at least one and every one lies
    /// within <paramref name="rectangle"/>.
    /// </summary>
    private static bool AreWithinRectangle(in PdfRectangle rectangle, PdfPoint[][] quadrilaterals)
    {
        if (quadrilaterals.Length == 0)
        {
            return false;
        }

        foreach (PdfPoint[] quadrilateral in quadrilaterals)
        {
            PdfRectangle? bounds = PdfRectangle.FromPoints(quadrilateral);

            if (bounds == null || !rectangle.Contains(bounds.Value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this link annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and destination or URI.</returns>
    public override string ToString()
    {
        if (Action is PdfUriAction uriAction && uriAction.Uri != null)
        {
            return $"Link Annotation: {uriAction.Uri}";
        }

        if (Action != null)
        {
            return $"Link Annotation: {Action.ActionType} Action";
        }

        return "Link Annotation";
    }
}
