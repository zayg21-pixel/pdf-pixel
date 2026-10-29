using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF squiggly underline annotation.
/// </summary>
public class PdfSquigglyAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquigglyAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this squiggly annotation.</param>
    internal PdfSquigglyAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Squiggly)
    {
    }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        PdfColor color = ResolveColor(page);

        foreach (PdfPoint[] quad in quads)
        {
            (PdfMatrix lineMatrix, float halfLength, float height) = GetBaselineFrame(quad);

            PdfPathBuilder path = new(lineMatrix);
            DrawSquigglyLine(path, -halfLength, halfLength, 0f, GetAmplitude(height));

            PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color);

            processor.Process(new DrawPathCommand(path.ToPath(), paint));
        }

        return true;
    }

    // Quadrilateral height divided by 6.
    private static float GetAmplitude(float height) => height / 6f;

    // Draws a wave that starts at (startX, baselineY + amplitude) and zigzags between the baseline
    // and one amplitude above it every 2 units of X, ending at or past endX.
    private static void DrawSquigglyLine(PdfPathBuilder path, float startX, float endX, float baselineY, float amplitude)
    {
        float x = startX;
        float shift = amplitude;

        path.MoveTo(x, baselineY + shift);

        do
        {
            x += 2f;
            shift = (shift == 0f) ? amplitude : 0f;
            path.LineTo(x, baselineY + shift);
        }
        while (x < endX);
    }

    /// <summary>
    /// Returns a string representation of this squiggly annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Squiggly Annotation: {Contents}";
        }

        return "Squiggly Annotation";
    }
}
