using PdfPixel.Commands;
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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        float width = Rectangle.Width;
        float height = Rectangle.Height;
        SKColor interiorSKColor = ResolveInteriorColor(page);

        if (interiorSKColor != SKColors.Transparent)
        {
            SKPaint fillPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = interiorSKColor
            };

            using SKPath fillPath = new();
            fillPath.AddRect(new SKRect(Rectangle.Left, Rectangle.Top, Rectangle.Left + width, Rectangle.Top + height));
            processor.Process(new DrawPathCommand(fillPath, fillPaint));
        }

        if (BorderStyle?.Width > 0 && Color?.Length > 0)
        {
            SKColor strokeColor = ResolveColor(page, SKColors.Black);

            SKPaint strokePaint = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderStyle.Width,
                Color = strokeColor
            };

            BorderStyle.TryApplyEffect(strokePaint, strokeColor);

            float halfBorder = BorderStyle.Width / 2;
            SKRect adjustedRect = new(
                Rectangle.Left + halfBorder,
                Rectangle.Top + halfBorder,
                Rectangle.Right - halfBorder,
                Rectangle.Bottom - halfBorder);

            using SKPath strokePath = new();
            strokePath.AddRect(adjustedRect);
            processor.Process(new DrawPathCommand(strokePath, strokePaint));
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this square annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Square Annotation: {contentsText}";
        }

        return "Square Annotation";
    }
}
