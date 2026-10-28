using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Measures;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF line annotation.
/// </summary>
public class PdfLineAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLineAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this line annotation.</param>
    internal PdfLineAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Line)
    {
        LeaderLineLength = annotationObject.Dictionary.GetFloat(PdfTokens.LeaderLineKey);
        LeaderLineExtension = annotationObject.Dictionary.GetFloat(PdfTokens.LeaderLineExtensionKey);
        LeaderLineOffset = annotationObject.Dictionary.GetFloat(PdfTokens.LeaderLineOffsetKey);

        PdfArray? lineArray = annotationObject.Dictionary.GetArray(PdfTokens.LKey);
        if (lineArray?.Count >= 4)
        {
            StartX = lineArray.GetFloatOrDefault(0);
            StartY = lineArray.GetFloatOrDefault(1);
            EndX = lineArray.GetFloatOrDefault(2);
            EndY = lineArray.GetFloatOrDefault(3);

            PdfPoint start = new(StartX, StartY);
            PdfPoint end = new(EndX, EndY);
            DrawingLine = PdfAnnotationGeometry.MoveLine(start, end, LeaderLineLength ?? 0f, LeaderLineOffset ?? 0f);
            DrawingPath = PdfAnnotationGeometry.CreatePolylinePath(DrawingLine);
            LeaderLinesPath = PdfAnnotationGeometry.CreateLeaderLinesPath(start, end, LeaderLineLength ?? 0f, LeaderLineExtension ?? 0f, LeaderLineOffset ?? 0f);
        }

        PdfArray? lineEndingArray = annotationObject.Dictionary.GetArray(PdfTokens.LineEndingKey);
        if (lineEndingArray?.Count >= 2)
        {
            StartLineEnding = lineEndingArray.GetNameOrDefault(0).AsEnum<PdfLineEndingStyle>();
            EndLineEnding = lineEndingArray.GetNameOrDefault(1).AsEnum<PdfLineEndingStyle>();
        }

        HasCaption = annotationObject.Dictionary.GetBooleanOrDefault(PdfTokens.CaptionKey);
        CaptionPosition = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.CaptionPositionKey).AsEnum<PdfLineCaptionPosition>();
        CaptionOffset = PdfPoint.FromArray(annotationObject.Dictionary.GetArray(PdfTokens.CaptionOffsetKey)) ?? PdfPoint.Empty;
        Measure = PdfMeasure.GetMeasure(annotationObject.Dictionary, PdfTokens.MeasureKey);
        StrokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the line's start coordinates.
    /// </summary>
    protected override PdfPoint ContentStart => PdfAnnotationGeometry.GetStartPoint(DrawingPath) ?? base.ContentStart;

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => !HasCaption && base.ShouldDisplayBubble;

    /// <summary>
    /// Gets the declared rectangle, or the stroked bounds of the line and its leader lines when there is
    /// no appearance stream and the declared rectangle does not overlap them.
    /// </summary>
    public override PdfRectangle Rectangle
    {
        get
        {
            if (Appearance != null)
            {
                return base.Rectangle;
            }

            PdfRectangle rectangle = PdfAnnotationGeometry.GetFallbackRectangle(base.Rectangle, DrawingPath, StrokeStyle.LineWidth);

            if (LeaderLinesPath != null)
            {
                rectangle = PdfRectangle.Union(rectangle, PdfAnnotationGeometry.GetFallbackRectangle(base.Rectangle, LeaderLinesPath, StrokeStyle.LineWidth));
            }

            return rectangle;
        }
    }

    /// <summary>
    /// Gets the X coordinate of the line's start point.
    /// </summary>
    public float StartX { get; }

    /// <summary>
    /// Gets the Y coordinate of the line's start point.
    /// </summary>
    public float StartY { get; }

    /// <summary>
    /// Gets the X coordinate of the line's end point.
    /// </summary>
    public float EndX { get; }

    /// <summary>
    /// Gets the Y coordinate of the line's end point.
    /// </summary>
    public float EndY { get; }

    /// <summary>
    /// Gets the line ending style at the start point.
    /// </summary>
    public PdfLineEndingStyle StartLineEnding { get; }

    /// <summary>
    /// Gets the line ending style at the end point.
    /// </summary>
    public PdfLineEndingStyle EndLineEnding { get; }

    /// <summary>
    /// Gets the leader line length.
    /// </summary>
    public float? LeaderLineLength { get; }

    /// <summary>
    /// Gets the leader line extension length.
    /// </summary>
    public float? LeaderLineExtension { get; }

    /// <summary>
    /// Gets the leader line offset.
    /// </summary>
    public float? LeaderLineOffset { get; }

    /// <summary>
    /// Gets the scale and units that apply to the line (/Measure), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMeasure? Measure { get; }

    /// <summary>
    /// Gets whether the Contents text is shown as a caption on the line (Cap entry). Default false.
    /// </summary>
    public bool HasCaption { get; }

    /// <summary>
    /// Gets the position of the caption relative to the line (CP entry). Default <see cref="PdfLineCaptionPosition.Inline"/>.
    /// </summary>
    public PdfLineCaptionPosition CaptionPosition { get; }

    /// <summary>
    /// Gets the offset of the caption from its normal position (CO entry): along the line from its midpoint, then
    /// perpendicular to it. Default (0, 0).
    /// </summary>
    public PdfPoint CaptionOffset { get; }

    /// <summary>
    /// Gets the stroke style used to draw the line, falling back to defaults when no BS/Border entry is present.
    /// </summary>
    public PdfStrokeStyle StrokeStyle { get; }

    /// <summary>
    /// Gets the start and end points of the line that is drawn, from the line coordinates moved out by the leader
    /// lines. Null when the line coordinates are absent.
    /// </summary>
    internal PdfPoint[]? DrawingLine { get; }

    /// <summary>
    /// Gets <see cref="DrawingLine"/> as a path. Null when the line coordinates are absent.
    /// </summary>
    internal PdfPath? DrawingPath { get; }

    /// <summary>
    /// Gets the leader lines that are drawn. Null when the line coordinates are absent or the leader line length is 0.
    /// </summary>
    internal PdfPath? LeaderLinesPath { get; }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        if (DrawingLine == null)
        {
            return false;
        }

        PdfPoint lineStart = DrawingLine[0];
        PdfPoint lineEnd = DrawingLine[1];
        PdfRectangle? captionArea = null;

        if (HasCaption && Contents != null)
        {
            captionArea = PdfAnnotationTextRenderer.DrawLineCaption(
                processor,
                page,
                Contents.Value.DecodePdfString(),
                lineStart,
                lineEnd,
                CaptionPosition,
                CaptionOffset,
                StrokeStyle.LineWidth,
                LeaderLineLength ?? 0f,
                LeaderLineExtension ?? 0f);
        }

        PdfColor lineColor = ResolveColor(page);

        PdfPaint linePaint = PdfAnnotationPaintFactory.CreateStrokePaint(lineColor, StrokeStyle);

        processor.Process(new DrawPathCommand(PdfAnnotationGeometry.CreateLinePath(lineStart, lineEnd, captionArea), linePaint));

        if (LeaderLinesPath != null)
        {
            PdfPaint leaderLinesPaint = PdfAnnotationPaintFactory.CreateStrokePaint(lineColor, StrokeStyle);
            processor.Process(new DrawPathCommand(LeaderLinesPath, leaderLinesPaint));
        }

        PdfColor interiorColor = ResolveInteriorColor(page);

        if (StartLineEnding != PdfLineEndingStyle.None)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                lineStart.X,
                lineStart.Y,
                lineEnd.X,
                lineEnd.Y,
                StartLineEnding,
                StrokeStyle.LineWidth,
                lineColor,
                interiorColor);
        }

        if (EndLineEnding != PdfLineEndingStyle.None)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                lineEnd.X,
                lineEnd.Y,
                lineStart.X,
                lineStart.Y,
                EndLineEnding,
                StrokeStyle.LineWidth,
                lineColor,
                interiorColor);
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this line annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Line Annotation: {Contents}";
        }

        return "Line Annotation";
    }
}
