using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.Commands;

/// <summary>
/// Helper methods for commands, e.g. for applying modifiers to paints or paths.
/// </summary>
internal class CommandHelpers
{
    /// <summary>
    /// Applies the modifiers to the given paint, returning a new paint with the modifiers applied.
    /// </summary>
    public static SKPaint ApplyModifiers(SKPaint paint, IEnumerable<IPdfCommandModifier> modifiers) // TODO: use more
    {
        var result = paint.Clone();

        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(result);
        }

        return result;
    }

    /// <summary>
    /// Returns SKCanvas's matrix scaled to <see cref="PdfRenderingParameters.ScaleFactor"/>.
    /// </summary>
    public static SKMatrix GetScaledMatrix(SKCanvas canvas, PdfCommandExecutionContext executionContext)
    {
        if (executionContext.RenderingParameters.ScaleFactor.HasValue)
        {
            var scaleValue = executionContext.RenderingParameters.ScaleFactor.Value;
            return canvas.TotalMatrix.PostConcat(SKMatrix.CreateScale(scaleValue, scaleValue));
        }

        return canvas.TotalMatrix;
    }

    public static bool GetPathIsAntialias(SKPath path, SKCanvas canvas, PdfCommandExecutionContext executionContext, SKPaint paint = null) // TODO: account for angled rects + lines
    {
        var scaledMatrix = GetScaledMatrix(canvas, executionContext);
        if ((path.IsRect || path.IsLine) && canvas.TotalMatrix.SkewX == 0 && canvas.TotalMatrix.SkewY == 0)
        {
            SKRect bounds;

            if (paint == null)
            {
                bounds = path.TightBounds;
            }
            else
            {
                if (paint.Style == SKPaintStyle.Stroke)
                {
                    var stroke = paint.StrokeWidth == 0 ? 1 : paint.StrokeWidth;
                    bounds = new SKRect(0, 0, stroke, stroke);
                }
                else
                {
                    var fillPath = paint.GetFillPath(path);
                    bounds = fillPath.Bounds;
                }
            }

            var scaledRect = scaledMatrix.MapRect(bounds);

            if (scaledRect.Width >= 2 && scaledRect.Height >= 2)
            {
                return false;
            }
        }

        return executionContext.RenderingParameters.Antialias;
    }
}
