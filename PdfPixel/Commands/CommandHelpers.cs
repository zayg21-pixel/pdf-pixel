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
    public static SKPaint ApplyModifiers(SKPaint paint, IEnumerable<IPdfCommandModifier> modifiers)
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

    public static bool GetPathIsAntialias(SKPath path, SKCanvas canvas, PdfCommandExecutionContext executionContext, SKPaint paint = null)
    {
        var scaledMatrix = GetScaledMatrix(canvas, executionContext);

        if (!PathIsAxisAligned(path, scaledMatrix))
            return true;

        // Stroke pass: thin strokes benefit from antialiasing
        if (paint != null && (paint.Style == SKPaintStyle.Stroke || paint.Style == SKPaintStyle.StrokeAndFill))
        {
            var stroke = paint.StrokeWidth == 0 ? 1f : paint.StrokeWidth;
            var scaledStroke = scaledMatrix.MapRect(new SKRect(0, 0, stroke, stroke));
            if (scaledStroke.Width < 2 || scaledStroke.Height < 2)
                return executionContext.RenderingParameters.Antialias;
        }

        // Fill pass: small fills benefit from antialiasing
        if (paint == null || paint.Style == SKPaintStyle.Fill || paint.Style == SKPaintStyle.StrokeAndFill)
        {
            SKRect bounds;
            if (paint != null)
            {
                using var fillPath = paint.GetFillPath(path);
                bounds = fillPath?.Bounds ?? path.TightBounds;
            }
            else
            {
                bounds = path.TightBounds;
            }

            var scaledRect = scaledMatrix.MapRect(bounds);
            if (scaledRect.Width < 2 || scaledRect.Height < 2)
                return executionContext.RenderingParameters.Antialias;
        }

        return false;
    }

    private const float AxisAlignEpsilon = 0.01f;

    private static bool PathIsAxisAligned(SKPath path, SKMatrix matrix)
    {
        using var iterator = path.CreateIterator(true); // forceClose ensures implicit closing segments are checked
        var points = new SKPoint[4];
        SKPathVerb verb;

        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Line:
                    var a = matrix.MapPoint(points[0]);
                    var b = matrix.MapPoint(points[1]);
                    if (MathF.Abs(b.X - a.X) > AxisAlignEpsilon && MathF.Abs(b.Y - a.Y) > AxisAlignEpsilon)
                        return false;
                    break;
                case SKPathVerb.Quad:
                case SKPathVerb.Conic:
                case SKPathVerb.Cubic:
                    return false;
            }
        }

        return true;
    }
}
