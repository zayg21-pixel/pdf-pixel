using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF square annotation.
/// </summary>
/// <remarks>
/// Square annotations display a rectangle on the page. When the annotation has no appearance stream,
/// the rectangle is drawn within the annotation rectangle using the specified color and border style.
/// </remarks>
public class PdfSquareAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquareAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this square annotation.</param>
    public PdfSquareAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Square)
    {
    }

    /// <summary>
    /// Creates a fallback rendering for square annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered square.</returns>
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

            canvas.DrawRect(Rectangle.Left, Rectangle.Top, width, height, fillPaint);
        }

        if (BorderStyle != null && BorderStyle.Width > 0 && Color != null && Color.Length > 0)
        {
            var strokeColor = ResolveColor(page, SKColors.Black);

            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderStyle.Width,
                IsAntialias = true,
                Color = strokeColor
            };

            BorderStyle.TryApplyEffect(strokePaint, strokeColor);

            var halfBorder = BorderStyle.Width / 2;
            var adjustedRect = new SKRect(
                Rectangle.Left + halfBorder,
                Rectangle.Top + halfBorder,
                Rectangle.Right - halfBorder,
                Rectangle.Bottom - halfBorder);

            canvas.DrawRect(adjustedRect, strokePaint);
        }

        return recorder.EndRecording();
    }

    /// <summary>
    /// Returns a string representation of this square annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Square Annotation: {contentsText}";
        }

        return "Square Annotation";
    }
}
