using PdfPixel.Annotations.Rendering;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF line annotation.
/// </summary>
/// <remarks>
/// Line annotations display a single straight line on the page. The line may have optional
/// line ending styles at its start and end points (such as arrows, circles, or diamonds).
/// </remarks>
public class PdfLineAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLineAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this line annotation.</param>
    public PdfLineAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Line)
    {
        var lineArray = annotationObject.Dictionary.GetArray(PdfTokens.LKey);
        if (lineArray != null && lineArray.Count >= 4)
        {
            StartX = lineArray.GetFloatOrDefault(0);
            StartY = lineArray.GetFloatOrDefault(1);
            EndX = lineArray.GetFloatOrDefault(2);
            EndY = lineArray.GetFloatOrDefault(3);
        }

        var lineEndingArray = annotationObject.Dictionary.GetArray(PdfTokens.LineEndingKey);
        if (lineEndingArray != null && lineEndingArray.Count >= 2)
        {
            StartLineEnding = lineEndingArray.GetName(0).AsEnum<PdfLineEndingStyle>();
            EndLineEnding = lineEndingArray.GetName(1).AsEnum<PdfLineEndingStyle>();
        }

        LeaderLineLength = annotationObject.Dictionary.GetFloat(PdfTokens.LeaderLineKey);
        LeaderLineExtension = annotationObject.Dictionary.GetFloat(PdfTokens.LeaderLineExtensionKey);
        LeaderLineOffset = annotationObject.Dictionary.GetFloat(PdfTokens.LeaderLineOffsetKey);
    }

    protected override SKPoint ContentStart => new SKPoint(StartX, StartY);

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
    /// <remarks>
    /// Leader lines extend perpendicular from the line endpoints. A positive value extends
    /// in the direction of the line, while a negative value extends in the opposite direction.
    /// </remarks>
    public float? LeaderLineLength { get; }

    /// <summary>
    /// Gets the leader line extension length.
    /// </summary>
    public float? LeaderLineExtension { get; }

    /// <summary>
    /// Gets the leader line offset.
    /// </summary>
    public float? LeaderLineOffset { get; }

    protected override bool RenderFallback(IPdfCommandProcessor processor, PdfPage page, PdfAnnotationVisualStateKind visualStateKind, PdfRenderingParameters renderingParameters)
    {
        var lineColor = ResolveColor(page, SKColors.Black);
        var lineWidth = BorderStyle?.Width ?? 1.0f;

        var linePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            StrokeCap = SKStrokeCap.Butt,
            IsAntialias = renderingParameters.Antialias,
            Color = lineColor
        };

        BorderStyle?.TryApplyEffect(linePaint, lineColor);

        using var linePath = new SKPath();
        linePath.MoveTo(StartX, StartY);
        linePath.LineTo(EndX, EndY);
        processor.Process(new DrawPathCommand(linePath, linePaint));

        var interiorSKColor = ResolveInteriorColor(page);

        if (StartLineEnding != PdfLineEndingStyle.None)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                StartX,
                StartY,
                EndX,
                EndY,
                StartLineEnding,
                lineWidth,
                lineColor,
                interiorSKColor,
                renderingParameters);
        }

        if (EndLineEnding != PdfLineEndingStyle.None)
        {
            PdfAnnotationLineEndingRenderer.DrawLineEnding(
                processor,
                EndX,
                EndY,
                StartX,
                StartY,
                EndLineEnding,
                lineWidth,
                lineColor,
                interiorSKColor,
                renderingParameters);
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this line annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Line Annotation: {contentsText}";
        }

        return "Line Annotation";
    }
}
