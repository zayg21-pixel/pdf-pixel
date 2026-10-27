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
using System;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF link annotation.
/// </summary>
/// <remarks>
/// Link annotations represent either hypertext links to destinations elsewhere in the document
/// or actions to be performed. They are typically invisible but may have a border or highlight effect.
/// </remarks>
public class PdfLinkAnnotation : PdfAnnotation
{
    private readonly PdfRectangle[]? _activeRectangles;

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
        Quadrilaterals = PdfAnnotationGeometryParser.ParseQuadrilaterals(annotationObject.Dictionary.GetArray(PdfTokens.QuadPointsKey));
        _activeRectangles = GetActiveRectangles(Rectangle, Quadrilaterals);
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <inheritdoc/>
    public override bool IsInteractive => true;

    /// <summary>
    /// Gets the quadrilaterals the link is active in, each as four points, e.g. one per line of a link
    /// spanning two lines of text. Empty when the link has none.
    /// </summary>
    public PdfPoint[][] Quadrilaterals { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// One rectangle per quadrilateral when every quadrilateral lies within <see cref="PdfAnnotation.Rectangle"/>.
    /// Otherwise <see cref="PdfAnnotation.Rectangle"/>.
    /// </remarks>
    public override PdfRectangle[] GetHoverRectangles(IPdfPage page)
    {
        if (_activeRectangles == null)
        {
            return base.GetHoverRectangles(page);
        }

        return _activeRectangles;
    }

    /// <summary>
    /// Gets the destination (/Dest) shown when the annotation is activated, resolved by this document's
    /// <see cref="PdfDestinationResolver"/>. Null if the link uses an Action instead; per PDF spec a link
    /// carries either a Dest or an A entry.
    /// </summary>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Gets the action dictionary that defines the action to be performed when the annotation is activated.
    /// </summary>
    /// <remarks>
    /// This property is null if the link uses a Destination instead.
    /// Common action types include GoTo, GoToR, URI, Launch, etc.
    /// </remarks>
    public PdfAction? Action { get; }

    /// <summary>
    /// Gets the highlight mode that specifies the visual effect to use when the link is activated.
    /// </summary>
    /// <remarks>
    /// Valid values are:
    /// - None: No highlighting
    /// - Invert: Invert the colors of the annotation rectangle (default)
    /// - Outline: Invert the border of the annotation rectangle
    /// - Push: Display the annotation as if it were being pushed
    /// </remarks>
    public PdfLinkHighlightMode HighlightMode { get; }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfStrokeStyle strokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();

        if (strokeStyle.LineWidth <= 0 || Color?.Length == 0)
        {
            return false;
        }

        PdfColor color = ResolveColor(page, PdfColors.Black);
        float borderWidth = strokeStyle.LineWidth;

        PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color, strokeStyle);

        if (_activeRectangles == null)
        {
            DrawBorder(processor, Rectangle, paint, borderWidth);
        }
        else
        {
            foreach (PdfRectangle activeRectangle in _activeRectangles)
            {
                DrawBorder(processor, activeRectangle, paint, borderWidth);
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

    private void DrawBorder(IPdfCommandProcessor processor, in PdfRectangle area, PdfPaint paint, float borderWidth)
    {
        // A border narrower than the rectangle's shorter side outlines it from the inside and already stays
        // within it. Anything thicker inverts that inset rectangle and would stroke across the surrounding
        // page, so only then is the border confined to the annotation.
        bool confineToRectangle = borderWidth > Math.Min(area.Width, area.Height);

        if (confineToRectangle)
        {
            processor.Process(SaveStateCommand.Instance);
            processor.Process(new ClipRectangleCommand(area, PdfClipOperation.Intersect));
        }

        if (BorderStyle?.StyleType == PdfAnnotationBorderStyleType.Underline)
        {
            float y = area.Top - (borderWidth / 2);
            PdfPathBuilder linePath = new();
            linePath.MoveTo(area.Left, y);
            linePath.LineTo(area.Right, y);
            processor.Process(new DrawPathCommand(linePath.ToPath(), paint));
        }
        else
        {
            PdfRectangle rect = new(
                area.Left + (borderWidth / 2),
                area.Top + (borderWidth / 2),
                area.Right - (borderWidth / 2),
                area.Bottom - (borderWidth / 2));

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(rect);
            processor.Process(new DrawPathCommand(rectPath.ToPath(), paint));
        }

        if (confineToRectangle)
        {
            processor.Process(RestoreStateCommand.Instance);
        }
    }

    /// <summary>
    /// Returns the bounding box of each quadrilateral when they define the active area: every quadrilateral
    /// lies within <paramref name="rectangle"/>.
    /// </summary>
    /// <returns>The rectangles, or null when <paramref name="rectangle"/> is the active area.</returns>
    private static PdfRectangle[]? GetActiveRectangles(in PdfRectangle rectangle, PdfPoint[][] quadrilaterals)
    {
        if (quadrilaterals.Length == 0)
        {
            return null;
        }

        var rectangles = new PdfRectangle[quadrilaterals.Length];

        for (int index = 0; index < quadrilaterals.Length; index++)
        {
            PdfRectangle? bounds = PdfRectangle.FromPoints(quadrilaterals[index]);

            if (bounds == null)
            {
                return null;
            }

            if (!rectangle.Contains(bounds.Value))
            {
                return null;
            }

            rectangles[index] = bounds.Value;
        }

        return rectangles;
    }
}
