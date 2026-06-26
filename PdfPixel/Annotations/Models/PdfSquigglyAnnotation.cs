using PdfPixel.Commands;
using PdfPixel.Models;
using SkiaSharp;
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
        SKPoint[][] quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        SKColor color = ResolveColor(page, SKColors.Red);

        foreach (SKPoint[] quad in quads)
        {
            float startX = quad[0].X;
            float startY = quad[0].Y;
            float endX = quad[1].X;
            float endY = quad[1].Y;

            using SKPathBuilder pathBuilder = new();
            DrawSquigglyLine(pathBuilder, startX, startY, endX, endY);

            SKPaint paint = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                Color = color
            };

            processor.Process(new DrawPathCommand(pathBuilder.Detach(), paint));
        }

        return true;
    }

    private static void DrawSquigglyLine(SKPathBuilder path, float startX, float startY, float endX, float endY)
    {
        var length = (float)Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
        const float waveHeight = 2.0f;
        const float waveLength = 4.0f;
        var waveCount = (int)(length / waveLength);

        if (waveCount < 1)
        {
            path.MoveTo(startX, startY);
            path.LineTo(endX, endY);
            return;
        }

        float dx = (endX - startX) / length;
        float dy = (endY - startY) / length;

        float perpDx = -dy;
        float perpDy = dx;

        path.MoveTo(startX, startY);

        for (int i = 0; i < waveCount; i++)
        {
            float t1 = (i + 0.5f) * waveLength;
            float t2 = (i + 1.0f) * waveLength;

            float x1 = startX + (dx * t1);
            float y1 = startY + (dy * t1);
            float x2 = startX + (dx * t2);
            float y2 = startY + (dy * t2);

            int offsetSign = (i % 2 == 0) ? 1 : -1;
            x1 += perpDx * waveHeight * offsetSign;
            y1 += perpDy * waveHeight * offsetSign;

            path.LineTo(x1, y1);
            path.LineTo(x2, y2);
        }

        if (waveCount * waveLength < length)
        {
            path.LineTo(endX, endY);
        }
    }

    /// <summary>
    /// Returns a string representation of this squiggly annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Squiggly Annotation: {contentsText}";
        }

        return "Squiggly Annotation";
    }
}
