using PdfPixel.Commands;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF circle annotation.
/// </summary>
/// <remarks>
/// Circle annotations display an ellipse on the page. When the annotation has no appearance stream,
/// the ellipse is drawn to fit within the annotation rectangle using the specified color and border style.
/// </remarks>
public class PdfCircleAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCircleAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this circle annotation.</param>
    public PdfCircleAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Circle)
    {
    }

    /// <summary>
    /// Renders the fallback content for circle annotations when no appearance stream is available.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>True if fallback rendering was emitted.</returns>
    public override bool RenderFallback(IPdfCommandProcessor processor, PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        var width = Rectangle.Width;
        var height = Rectangle.Height;
        var interiorSKColor = ResolveInteriorColor(page);

        var centerX = Rectangle.Left + width / 2;
        var centerY = Rectangle.Top + height / 2;

        if (interiorSKColor != SKColors.Transparent)
        {
            var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true, // TODO: take from rendering parameters
                Color = interiorSKColor
            };

            using var fillPath = new SKPath();
            fillPath.AddOval(new SKRect(centerX - width / 2, centerY - height / 2, centerX + width / 2, centerY + height / 2));
            processor.Process(new DrawPathCommand(fillPath, fillPaint));
        }

        if (BorderStyle != null && BorderStyle.Width > 0 && Color != null && Color.Length > 0)
        {
            var borderWidth = BorderStyle.Width;
            var strokeColor = ResolveColor(page, SKColors.Black);

            var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth,
                IsAntialias = true,
                Color = strokeColor
            };

            BorderStyle.TryApplyEffect(strokePaint, strokeColor);

            var adjustedWidth = width - BorderStyle.Width;
            var adjustedHeight = height - BorderStyle.Width;

            using var strokePath = new SKPath();
            strokePath.AddOval(new SKRect(
                centerX - adjustedWidth / 2,
                centerY - adjustedHeight / 2,
                centerX + adjustedWidth / 2,
                centerY + adjustedHeight / 2));
            processor.Process(new DrawPathCommand(strokePath, strokePaint));
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this circle annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Circle Annotation: {contentsText}";
        }

        return "Circle Annotation";
    }
}
