using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;

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
        BorderEffect = PdfAnnotationBorderParser.ParseBorderEffect(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
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
    /// Gets the parsed border effect (BE entry).
    /// </summary>
    public PdfAnnotationBorderEffect BorderEffect { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        float width = ContentRectangle.Width;
        float height = ContentRectangle.Height;
        PdfColor interiorColor = ResolveInteriorColor(page);

        float centerX = ContentRectangle.Left + (width / 2);
        float centerY = ContentRectangle.Top + (height / 2);

        if (interiorColor != PdfColors.Transparent)
        {
            PdfPathBuilder fillPath = new();
            fillPath.AddOval(new PdfRectangle(centerX - (width / 2), centerY - (height / 2), centerX + (width / 2), centerY + (height / 2)));
            processor.Process(new DrawPathCommand(fillPath.ToPath(), PdfAnnotationPaintFactory.CreateFillPaint(interiorColor)));
        }

        if (BorderStyle != null && BorderStyle.StrokeStyle.LineWidth > 0 && Color?.Length > 0)
        {
            PdfColor strokeColor = ResolveColor(page, PdfColors.Black);

            PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(strokeColor, BorderStyle.StrokeStyle, BorderEffect);

            float adjustedWidth = width - BorderStyle.StrokeStyle.LineWidth;
            float adjustedHeight = height - BorderStyle.StrokeStyle.LineWidth;

            PdfPathBuilder strokePath = new();
            strokePath.AddOval(new PdfRectangle(
                centerX - (adjustedWidth / 2),
                centerY - (adjustedHeight / 2),
                centerX + (adjustedWidth / 2),
                centerY + (adjustedHeight / 2)));
            processor.Process(new DrawPathCommand(strokePath.ToPath(), strokePaint));
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this circle annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Circle Annotation: {Contents}";
        }

        return "Circle Annotation";
    }
}
