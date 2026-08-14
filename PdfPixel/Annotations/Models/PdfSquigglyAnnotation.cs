using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF squiggly underline annotation.
/// </summary>
/// <remarks>
/// Squiggly annotations mark text with a wavy line drawn under it, typically
/// used to indicate spelling or grammar errors.
/// </remarks>
public class PdfSquigglyAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquigglyAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this squiggly annotation.</param>
    public PdfSquigglyAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Squiggly)
    {
    }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        PdfPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        PdfColor color = ResolveColor(page, PdfColors.Red);

        foreach (PdfPoint[] quad in quads)
        {
            float startX = quad[1].X;
            float endX = quad[0].X;
            float baselineY = quad[0].Y;
            float amplitude = GetAmplitude(quad);

            PdfPathBuilder path = new();
            DrawSquigglyLine(path, startX, endX, baselineY, amplitude);

            PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color);

            processor.Process(new DrawPathCommand(path.ToPath(), paint));
        }

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Padded by twice <see cref="GetAmplitude"/> above and below the baseline.
    /// </remarks>
    protected override PdfRectangle GetQuadBounds(PdfPoint[] quad)
    {
        if (quad == null)
        {
            throw new ArgumentNullException(nameof(quad));
        }

        float baselineY = quad[0].Y;
        float amplitude = GetAmplitude(quad);
        float left = Math.Min(quad[0].X, quad[1].X);
        float right = Math.Max(quad[0].X, quad[1].X);

        return new PdfRectangle(left, baselineY - (2f * amplitude), right, baselineY + (2f * amplitude));
    }

    // Quadrilateral height divided by 6.
    private static float GetAmplitude(PdfPoint[] quad) => (quad[2].Y - quad[0].Y) / 6f;

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
        if (Contents?.IsEmpty == false)
        {
            return $"Squiggly Annotation: {Contents}";
        }

        return "Squiggly Annotation";
    }
}
