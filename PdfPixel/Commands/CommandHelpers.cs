using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
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
    /// Returns the stroke width to assign for <paramref name="paint"/>: unchanged
    /// for fill paints and hairlines (<see cref="PdfStrokeStyle.LineWidth"/> &lt;= 0), otherwise the paint's
    /// line width clamped to the user-space width whose image under the current device matrix measures exactly
    /// one device pixel along its narrowest axis. Clamping keeps thin lines visible at low zoom, matching the
    /// minimum-line-width behavior of other PDF viewers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetMinimumStrokeWidth(PdfCommandExecutionContext executionContext, PdfPaint paint)
    {
        if (paint.Style != PdfPaintStyle.Stroke || paint.RequireStrokeStyle().LineWidth <= 0)
        {
            return 0f;
        }

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

        float minimumStrokeWidth = (absDeterminant <= 0) ? 1f : Math.Max(normX, normY) / absDeterminant;

        return Math.Max(paint.RequireStrokeStyle().LineWidth, minimumStrokeWidth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetPathIsAntialias(PdfPath path, PdfCommandExecutionContext executionContext, PdfPaint? paint = null)
    {
        if (!executionContext.Parameters.Antialias)
        {
            return false;
        }

        PdfMatrix scaledMatrix = GetScaledMatrix(executionContext);

        if (!PathIsAxisAligned(path, scaledMatrix))
        {
            return true;
        }

        // Stroke pass: thin strokes benefit from antialiasing
        if (paint != null && paint.Style == PdfPaintStyle.Stroke)
        {
            float stroke = GetMinimumStrokeWidth(executionContext, paint);
            if (stroke == 0)
            {
                stroke = 1f;
            }

            PdfRectangle scaledStroke = scaledMatrix.MapRect(new PdfRectangle(0, 0, stroke, stroke));
            if (scaledStroke.Width < 2 || scaledStroke.Height < 2)
            {
                return executionContext.Parameters.Antialias;
            }
        }

        // Fill pass: small fills benefit from antialiasing
        if (paint == null || paint.Style == PdfPaintStyle.Fill)
        {
            PdfRectangle bounds = path.GetBounds();

            PdfRectangle scaledRect = scaledMatrix.MapRect(bounds);
            if (scaledRect.Width < 2 || scaledRect.Height < 2)
            {
                return executionContext.Parameters.Antialias;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetRectIsAntialias(in PdfRectangle rect, PdfCommandExecutionContext executionContext)
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

        PdfRectangle scaledRect = scaledMatrix.MapRect(rect);
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
    public static PdfRectangle GetPixelSnappedRect(in PdfRectangle rect, PdfCommandExecutionContext executionContext)
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

        PdfRectangle deviceRect = scaledMatrix.MapRect(rect);
        PdfRectangle snappedDeviceRect = SnapToDevicePixels(deviceRect);

        return scaledMatrix.Invert().MapRect(snappedDeviceRect);
    }

    /// <summary>
    /// Formats a matrix in short PDF <c>[a b c d e f]</c> operand order, for debugging.
    /// </summary>
    public static string FormatMatrix(in PdfMatrix matrix)
        => $"[{matrix.ScaleX:0.###} {matrix.SkewY:0.###} {matrix.SkewX:0.###} {matrix.ScaleY:0.###} {matrix.TransX:0.###} {matrix.TransY:0.###}]";

    /// <summary>
    /// Formats a paint's blend mode, color, and style, for debugging.
    /// </summary>
    public static string FormatPaint(PdfPaint paint)
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
    public static PdfRectangle SnapToDevicePixels(in PdfRectangle deviceRect)
    {
        (float left, float right) = SnapDimensionToWholePixels(deviceRect.Left, deviceRect.Right);
        (float top, float bottom) = SnapDimensionToWholePixels(deviceRect.Top, deviceRect.Bottom);

        return new PdfRectangle(left, top, right, bottom);
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
    private static bool PathIsAxisAligned(PdfPath path, in PdfMatrix matrix)
    {
        PdfPoint currentPoint = default;
        PdfPoint subpathStart = default;

        foreach (PdfPathSegment segment in path.Segments)
        {
            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    currentPoint = segment.Points[0];
                    subpathStart = currentPoint;
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    if (!SegmentIsAxisAligned(matrix, currentPoint, segment.Points[0]))
                    {
                        return false;
                    }

                    currentPoint = segment.Points[0];
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    return false;
                }
                case PdfPathSegmentType.Close:
                {
                    // forceClose: an implicit closing line back to the subpath start must also be axis-aligned.
                    if (!SegmentIsAxisAligned(matrix, currentPoint, subpathStart))
                    {
                        return false;
                    }

                    currentPoint = subpathStart;
                    break;
                }
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SegmentIsAxisAligned(in PdfMatrix matrix, in PdfPoint start, in PdfPoint end)
    {
        PdfPoint a = matrix.MapPoint(start);
        PdfPoint b = matrix.MapPoint(end);
        return MathF.Abs(b.X - a.X) <= AxisAlignEpsilon || MathF.Abs(b.Y - a.Y) <= AxisAlignEpsilon;
    }
}
