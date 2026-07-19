using PdfPixel.Commands;
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
        RectDifferences = PdfRectangle.FromArray(annotationObject.Dictionary.GetArray(PdfTokens.RectDifferencesKey));
        ContentRectangle = ApplyRectDifferences(Rectangle, RectDifferences);
        BorderEffect = PdfBorderEffect.FromDictionary(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
    }

    /// <summary>
    /// Gets the rectangle differences that inset the drawn ellipse from the annotation rectangle.
    /// </summary>
    public PdfRectangle? RectDifferences { get; }

    /// <summary>
    /// Gets the effective drawing rectangle after applying <see cref="RectDifferences"/>.
    /// </summary>
    public PdfRectangle ContentRectangle { get; }

    /// <summary>
    /// Gets the border effect applied to this annotation's border, or null for no effect.
    /// </summary>
    public PdfBorderEffect? BorderEffect { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        float width = ContentRectangle.Width;
        float height = ContentRectangle.Height;
        SKColor interiorSKColor = ResolveInteriorColor(page);

        float centerX = ContentRectangle.Left + (width / 2);
        float centerY = ContentRectangle.Top + (height / 2);

        if (interiorSKColor != SKColors.Transparent)
        {
            SKPaint fillPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = interiorSKColor
            };

            using SKPathBuilder fillPathBuilder = new();
            fillPathBuilder.AddOval(new SKRect(centerX - (width / 2), centerY - (height / 2), centerX + (width / 2), centerY + (height / 2)));
            processor.Process(new DrawPathCommand(fillPathBuilder.Detach(), fillPaint));
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
            BorderEffect?.TryApplyEffect(strokePaint, borderWidth);

            float adjustedWidth = width - BorderStyle.Width;
            float adjustedHeight = height - BorderStyle.Width;

            using SKPathBuilder strokePathBuilder = new();
            strokePathBuilder.AddOval(new SKRect(
                centerX - (adjustedWidth / 2),
                centerY - (adjustedHeight / 2),
                centerX + (adjustedWidth / 2),
                centerY + (adjustedHeight / 2)));
            processor.Process(new DrawPathCommand(strokePathBuilder.Detach(), strokePaint));
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
