using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Geometry;
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
        RectDifferences = PdfRectangle.FromArray(annotationObject.Dictionary.GetArray(PdfTokens.RectDifferencesKey));
        ContentRectangle = ApplyRectDifferences(Rectangle, RectDifferences);
        BorderEffect = PdfBorderEffect.FromDictionary(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
    }

    /// <summary>
    /// Gets the rectangle differences that inset the drawn shape from the annotation rectangle.
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
        PdfColor interiorColor = ResolveInteriorColor(page);

        if (interiorColor != PdfColors.Transparent)
        {
            SKPaint fillPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = interiorColor.ToSkiaColor()
            };

            PdfPath fillPath = new();
            fillPath.AddRect(ContentRectangle);
            processor.Process(new DrawPathCommand(fillPath, fillPaint));
        }

        if (BorderStyle?.Width > 0 && Color?.Length > 0)
        {
            PdfColor strokeColor = ResolveColor(page, PdfColors.Black);

            SKPaint strokePaint = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderStyle.Width,
                Color = strokeColor.ToSkiaColor()
            };

            BorderStyle.TryApplyEffect(strokePaint, strokeColor);
            BorderEffect?.TryApplyEffect(strokePaint, BorderStyle.Width);

            float halfBorder = BorderStyle.Width / 2f;
            PdfRectangle adjustedRect = new(
                ContentRectangle.Left + halfBorder,
                ContentRectangle.Top + halfBorder,
                ContentRectangle.Right - halfBorder,
                ContentRectangle.Bottom - halfBorder);

            PdfPath strokePath = new();
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
