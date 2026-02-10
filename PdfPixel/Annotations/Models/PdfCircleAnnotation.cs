using PdfPixel.Color.ColorSpace;
using PdfPixel.Models;
using PdfPixel.Text;
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
    /// Creates a fallback rendering for circle annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered circle.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var width = Rectangle.Width;
        var height = Rectangle.Height;
        var interiorSKColor = ResolveInteriorColor(page);

        if (interiorSKColor != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = interiorSKColor
            };

            var centerX = Rectangle.Left + width / 2;
            var centerY = Rectangle.Top + height / 2;
            canvas.DrawOval(centerX, centerY, width / 2, height / 2, fillPaint);
        }

        if (BorderStyle != null && BorderStyle.Width > 0 && Color != null && Color.Length > 0)
        {
            var borderWidth = BorderStyle.Width;
            var strokeColor = ResolveColor(page, SKColors.Black);

            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth,
                IsAntialias = true,
                Color = strokeColor
            };

            BorderStyle.TryApplyEffect(strokePaint, strokeColor);

            var halfBorder = BorderStyle.Width / 2;
            var adjustedWidth = width - BorderStyle.Width;
            var adjustedHeight = height - BorderStyle.Width;

            var centerX = Rectangle.Left + width / 2;
            var centerY = Rectangle.Top + height / 2;
            canvas.DrawOval(
                centerX,
                centerY,
                adjustedWidth / 2,
                adjustedHeight / 2,
                strokePaint);
        }

        return recorder.EndRecording();
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
