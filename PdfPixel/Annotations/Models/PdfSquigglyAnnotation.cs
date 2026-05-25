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

    protected override bool RenderFallback(IPdfCommandProcessor processor, PdfPage page, PdfAnnotationVisualStateKind visualStateKind, PdfRenderingParameters renderingParameters)
    {
        var quads = Quadrilaterals;
        if (quads.Length == 0)
        {
            return false;
        }

        var color = ResolveColor(page, SKColors.Red);

        foreach (var quad in quads)
        {
            var startX = quad[0].X;
            var startY = quad[0].Y;
            var endX = quad[1].X;
            var endY = quad[1].Y;

            using var path = new SKPath();
            DrawSquigglyLine(path, startX, startY, endX, endY);

            var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                Color = color,
                IsAntialias = renderingParameters.Antialias
            };

            processor.Process(new DrawPathCommand(path, paint));
        }

        return true;
    }

    private static void DrawSquigglyLine(SKPath path, float startX, float startY, float endX, float endY)
    {
        var length = (float)Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
        var waveHeight = 2.0f;
        var waveLength = 4.0f;
        var waveCount = (int)(length / waveLength);

        if (waveCount < 1)
        {
            path.MoveTo(startX, startY);
            path.LineTo(endX, endY);
            return;
        }

        var dx = (endX - startX) / length;
        var dy = (endY - startY) / length;

        var perpDx = -dy;
        var perpDy = dx;

        path.MoveTo(startX, startY);

        for (int i = 0; i < waveCount; i++)
        {
            var t1 = (i + 0.5f) * waveLength;
            var t2 = (i + 1.0f) * waveLength;

            var x1 = startX + dx * t1;
            var y1 = startY + dy * t1;
            var x2 = startX + dx * t2;
            var y2 = startY + dy * t2;

            var offsetSign = (i % 2 == 0) ? 1 : -1;
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
        var contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Squiggly Annotation: {contentsText}";
        }

        return "Squiggly Annotation";
    }
}
