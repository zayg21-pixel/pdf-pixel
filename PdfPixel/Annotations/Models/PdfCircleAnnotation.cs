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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        float width = Rectangle.Width;
        float height = Rectangle.Height;
        SKColor interiorSKColor = ResolveInteriorColor(page);

        float centerX = Rectangle.Left + (width / 2);
        float centerY = Rectangle.Top + (height / 2);

        if (interiorSKColor != SKColors.Transparent)
        {
            SKPaint fillPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = interiorSKColor
            };

            using SKPath fillPath = new();
            fillPath.AddOval(new SKRect(centerX - (width / 2), centerY - (height / 2), centerX + (width / 2), centerY + (height / 2)));
            processor.Process(new DrawPathCommand(fillPath, fillPaint));
        }

        if (BorderStyle?.Width > 0 && Color?.Length > 0)
        {
            float borderWidth = BorderStyle.Width;
            SKColor strokeColor = ResolveColor(page, SKColors.Black);

            SKPaint strokePaint = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth,
                Color = strokeColor
            };

            BorderStyle.TryApplyEffect(strokePaint, strokeColor);

            float adjustedWidth = width - BorderStyle.Width;
            float adjustedHeight = height - BorderStyle.Width;

            using SKPath strokePath = new();
            strokePath.AddOval(new SKRect(
                centerX - (adjustedWidth / 2),
                centerY - (adjustedHeight / 2),
                centerX + (adjustedWidth / 2),
                centerY + (adjustedHeight / 2)));
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
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Circle Annotation: {contentsText}";
        }

        return "Circle Annotation";
    }
}
