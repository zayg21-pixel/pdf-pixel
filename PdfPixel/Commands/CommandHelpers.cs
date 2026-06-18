using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.Commands;

/// <summary>
/// Helper methods for commands, e.g. for applying modifiers to paints or paths.
/// </summary>
internal static class CommandHelpers
{
    /// <summary>
    /// Applies the modifiers to the given paint, returning a new paint with the modifiers applied.
    /// </summary>
    public static SKPaint ApplyModifiers(SKPaint paint, IEnumerable<IPdfCommandModifier> modifiers)
    {
        SKPaint result = paint.Clone();

        foreach (IPdfCommandModifier modifier in modifiers)
        {
            modifier.ModifyPaint(result);
        }

        return result;
    }

    /// <summary>
    /// Returns the command-derived total matrix scaled to <see cref="PdfPixel.Models.PdfCommandExecutionParameters.ScaleFactor"/>.
    /// </summary>
    public static SKMatrix GetScaledMatrix(PdfCommandExecutionContext executionContext)
    {
        SKMatrix totalMatrix = executionContext.Frames.TotalMatrix;

        if (executionContext.Parameters.ScaleFactor.HasValue)
        {
            float scaleValue = executionContext.Parameters.ScaleFactor.Value;
            return totalMatrix.PostConcat(SKMatrix.CreateScale(scaleValue, scaleValue));
        }

        return totalMatrix;
    }

    public static bool GetPathIsAntialias(SKPath path, PdfCommandExecutionContext executionContext, SKPaint? paint = null)
    {
        if (!executionContext.Parameters.Antialias)
        {
            return false;
        }

        SKMatrix scaledMatrix = GetScaledMatrix(executionContext);

        if (!PathIsAxisAligned(path, scaledMatrix))
        {
            return true;
        }

        // Stroke pass: thin strokes benefit from antialiasing
        if (paint != null && (paint.Style == SKPaintStyle.Stroke || paint.Style == SKPaintStyle.StrokeAndFill))
        {
            float stroke = (paint.StrokeWidth == 0) ? 1f : paint.StrokeWidth;
            SKRect scaledStroke = scaledMatrix.MapRect(new SKRect(0, 0, stroke, stroke));
            if (scaledStroke.Width < 2 || scaledStroke.Height < 2)
            {
                return executionContext.Parameters.Antialias;
            }
        }

        // Fill pass: small fills benefit from antialiasing
        if (paint == null || paint.Style == SKPaintStyle.Fill || paint.Style == SKPaintStyle.StrokeAndFill)
        {
            SKRect bounds;
            if (paint != null)
            {
                using SKPath fillPath = paint.GetFillPath(path);
                bounds = fillPath?.Bounds ?? path.TightBounds;
            }
            else
            {
                bounds = path.TightBounds;
            }

            SKRect scaledRect = scaledMatrix.MapRect(bounds);
            if (scaledRect.Width < 2 || scaledRect.Height < 2)
            {
                return executionContext.Parameters.Antialias;
            }
        }

        return false;
    }

    private const float AxisAlignEpsilon = 0.01f;

    private static bool PathIsAxisAligned(SKPath path, SKMatrix matrix)
    {
        using SKPath.Iterator iterator = path.CreateIterator(true); // forceClose ensures implicit closing segments are checked
        var points = new SKPoint[4];
        SKPathVerb verb;

        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Line:
                    {
                        SKPoint a = matrix.MapPoint(points[0]);
                        SKPoint b = matrix.MapPoint(points[1]);
                        if (MathF.Abs(b.X - a.X) > AxisAlignEpsilon && MathF.Abs(b.Y - a.Y) > AxisAlignEpsilon)
                        {
                            return false;
                        }

                        break;
                    }
                case SKPathVerb.Quad:
                case SKPathVerb.Conic:
                case SKPathVerb.Cubic:
                    return false;
            }
        }

        return true;
    }
}
