using PdfPixel.Geometry;
using SkiaSharp;
using System;
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
    public static PdfMatrix GetScaledMatrix(PdfCommandExecutionContext executionContext)
    {
        PdfMatrix totalMatrix = executionContext.Frames.TotalMatrix;

        if (executionContext.Parameters.ScaleFactor.HasValue)
        {
            float scaleValue = executionContext.Parameters.ScaleFactor.Value;
            return totalMatrix.PostConcat(PdfMatrix.CreateScale(scaleValue, scaleValue));
        }

        return totalMatrix;
    }

    /// <summary>
    /// Returns the user-space stroke width whose image under the current device matrix
    /// measures exactly one device pixel along its narrowest axis. Clamping a stroke width
    /// to at least this value keeps thin lines visible at low zoom, matching the minimum-line-width
    /// behavior of other PDF viewers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetMinimumStrokeWidth(PdfCommandExecutionContext executionContext)
    {
        PdfMatrix matrix = GetScaledMatrix(executionContext);
        PdfPoint origin = matrix.MapPoint(PdfPoint.Empty);
        PdfPoint mappedX = matrix.MapPoint(new PdfPoint(1, 0));
        PdfPoint mappedY = matrix.MapPoint(new PdfPoint(0, 1));

        float axisXx = mappedX.X - origin.X;
        float axisXy = mappedX.Y - origin.Y;
        float axisYx = mappedY.X - origin.X;
        float axisYy = mappedY.Y - origin.Y;

        float normX = MathF.Sqrt((axisXx * axisXx) + (axisXy * axisXy));
        float normY = MathF.Sqrt((axisYx * axisYx) + (axisYy * axisYy));
        float absDeterminant = Math.Abs((axisXx * axisYy) - (axisXy * axisYx));

        if (absDeterminant <= 0)
        {
            return 1f;
        }

        return Math.Max(normX, normY) / absDeterminant;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetPathIsAntialias(SKPath path, PdfCommandExecutionContext executionContext, SKPaint? paint = null)
    {
        if (!executionContext.Parameters.Antialias)
        {
            return false;
        }

        SKMatrix scaledMatrix = GetScaledMatrix(executionContext).ToSkMatrix();

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

        PdfMatrix scaledMatrix = GetScaledMatrix(executionContext);

        if (!IsAxisAligned(scaledMatrix))
        {
            return true;
        }

        SKRect scaledRect = scaledMatrix.ToSkMatrix().MapRect(rect);
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
        if (!executionContext.Parameters.SnapToDevicePixels)
        {
            return rect;
        }

        PdfMatrix scaledMatrix = GetScaledMatrix(executionContext);

        if (!IsAxisAligned(scaledMatrix))
        {
            return rect;
        }

        SKMatrix skScaledMatrix = scaledMatrix.ToSkMatrix();
        SKRect deviceRect = skScaledMatrix.MapRect(rect);
        SKRect snappedDeviceRect = SnapToDevicePixels(deviceRect);

        return skScaledMatrix.Invert().MapRect(snappedDeviceRect);
    }

    /// <summary>
    /// Formats a matrix in short PDF <c>[a b c d e f]</c> operand order, for debugging.
    /// </summary>
    public static string FormatMatrix(in PdfMatrix matrix)
        => $"[{matrix.ScaleX:0.###} {matrix.SkewY:0.###} {matrix.SkewX:0.###} {matrix.ScaleY:0.###} {matrix.TransX:0.###} {matrix.TransY:0.###}]";

    /// <summary>
    /// Formats a paint's blend mode, color, and style, for debugging.
    /// </summary>
    public static string FormatPaint(SKPaint paint)
        => $"{paint.BlendMode}/{paint.Color}/{paint.Style}";

    /// <summary>
    /// Returns whether <paramref name="matrix"/> has no rotation or skew, within <see cref="AxisAlignEpsilon"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAxisAligned(in PdfMatrix matrix)
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
