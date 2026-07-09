using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands;

/// <summary>
/// Helper methods for commands, e.g. for applying modifiers to paints or paths.
/// </summary>
internal static class CommandHelpers
{
    private const float AxisAlignEpsilon = 0.01f;

    /// <summary>
    /// Applies the modifiers to the given paint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyModifiers(SKPaint paint, PdfCommandExecutionContext context)
    {
        if (context.UncoloredModifier != null)
        {
            context.UncoloredModifier.ModifyPaint(paint);
        }
    }

    /// <summary>
    /// Returns the command-derived total matrix scaled to <see cref="PdfPixel.Models.PdfCommandExecutionParameters.ScaleFactor"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetRectIsAntialias(SKRect rect, PdfCommandExecutionContext executionContext)
    {
        if (!executionContext.Parameters.Antialias)
        {
            return false;
        }

        SKMatrix scaledMatrix = GetScaledMatrix(executionContext);

        if (!IsAxisAligned(scaledMatrix))
        {
            return true;
        }

        SKRect scaledRect = scaledMatrix.MapRect(rect);
        if (scaledRect.Width < 2 || scaledRect.Height < 2)
        {
            return executionContext.Parameters.Antialias;
        }

        return false;
    }

    /// <summary>
    /// Snaps <paramref name="rect"/> to whole device pixels when the command-derived total
    /// matrix is axis-aligned, returning it unchanged otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKRect GetPixelSnappedRect(SKRect rect, PdfCommandExecutionContext executionContext)
    {
        SKMatrix scaledMatrix = GetScaledMatrix(executionContext);

        if (!IsAxisAligned(scaledMatrix))
        {
            return rect;
        }

        SKRect deviceRect = scaledMatrix.MapRect(rect);
        SKRect snappedDeviceRect = SnapToDevicePixels(deviceRect);

        return scaledMatrix.Invert().MapRect(snappedDeviceRect);
    }

    /// <summary>
    /// Returns whether <paramref name="matrix"/> has no rotation or skew, within <see cref="AxisAlignEpsilon"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAxisAligned(SKMatrix matrix)
        => MathF.Abs(matrix.SkewX) <= AxisAlignEpsilon && MathF.Abs(matrix.SkewY) <= AxisAlignEpsilon;

    /// <summary>
    /// Snaps <paramref name="deviceRect"/> to whole device pixels, with a minimum size of one
    /// device pixel per dimension.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKRect SnapToDevicePixels(SKRect deviceRect)
    {
        (float left, float right) = SnapDimensionToWholePixels(deviceRect.Left, deviceRect.Right);
        (float top, float bottom) = SnapDimensionToWholePixels(deviceRect.Top, deviceRect.Bottom);

        return new SKRect(left, top, right, bottom);
    }

    /// <summary>
    /// Snaps a [<paramref name="low"/>, <paramref name="high"/>) range to whole pixel
    /// boundaries, with a minimum size of one pixel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (float Low, float High) SnapDimensionToWholePixels(float low, float high)
    {
        if (high - low < 1)
        {
            float snappedLow = MathF.Floor(low);
            return (snappedLow, snappedLow + 1);
        }

        float roundedLow = MathF.Round(low);
        float roundedHigh = MathF.Round(high);

        return (roundedLow, roundedHigh);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
