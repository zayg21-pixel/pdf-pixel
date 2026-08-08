using PdfPixel.Color.Paint;
using PdfPixel.Commands.Cache;
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
    /// Returns whether filling a path described by <paramref name="pathInfo"/> with <paramref name="paint"/>
    /// would cover no area at all, because the paint fills and the path's bounds are zero-width or
    /// zero-height, as produced by the degenerate <c>re f</c> rectangles some PDF producers use to draw
    /// grid lines. Such a path is drawn as a hairline stroke instead, so that it stays visible.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDegenerateFill(in PdfPathInfo pathInfo, PdfPaint paint)
    {
        if (paint.Style != PdfPaintStyle.Fill)
        {
            return false;
        }

        return pathInfo.Bounds.Width == 0 || pathInfo.Bounds.Height == 0;
    }

    /// <summary>
    /// Returns whether a path described by <paramref name="pathInfo"/> can be put on whole device pixels:
    /// it has to be a rectangle, under a matrix that keeps the axes where they are, with snapping asked
    /// for. A shape that is not a rectangle holds members thinner than its bounds — the outline a
    /// producer emitted in place of a stroke, most of all — and rounding those away would lose them.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanSnapPath(in PdfPathInfo pathInfo, PdfCommandExecutionContext executionContext)
    {
        return executionContext.Parameters.SnapToDevicePixels
            && pathInfo.IsRectangle
            && IsGridPreserving(GetScaledMatrix(executionContext));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetPathIsAntialias(in PdfPathInfo pathInfo, PdfCommandExecutionContext executionContext, PdfPaint? paint, in PdfStrokeScale strokeScale)
    {
        if (!executionContext.Parameters.Antialias)
        {
            return false;
        }

        PdfMatrix scaledMatrix = GetScaledMatrix(executionContext);

        if (!pathInfo.IsRectilinear || !IsGridPreserving(scaledMatrix))
        {
            return true;
        }

        // Stroke pass: thin strokes benefit from antialiasing
        if (paint != null && paint.Style == PdfPaintStyle.Stroke)
        {
            PdfRectangle scaledStroke = scaledMatrix.MapRect(new PdfRectangle(0, 0, strokeScale.PenWidth * strokeScale.X, strokeScale.PenWidth * strokeScale.Y));
            if (scaledStroke.Width < 2 || scaledStroke.Height < 2)
            {
                return true;
            }
        }

        if (paint == null || paint.Style == PdfPaintStyle.Fill)
        {
            // A fill that is not a rectangle covers less than its bounds, and what it leaves out can be
            // thinner than a pixel — the frame a producer drew as a fill rather than a stroke. Coverage
            // is what keeps those members on the page.
            if (!pathInfo.IsRectangle)
            {
                return true;
            }

            // Fill pass: a fill too small to survive rounding is left to coverage instead.
            PdfRectangle scaledRect = scaledMatrix.MapRect(pathInfo.Bounds);
            if (scaledRect.Width < 2 || scaledRect.Height < 2)
            {
                return true;
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

        return SnapRectToDevicePixels(rect, scaledMatrix);
    }

    /// <summary>
    /// Snaps <paramref name="rect"/> to whole pixels of the space <paramref name="deviceMatrix"/> maps
    /// into, and returns it in the space it was given in. A side thinner than a pixel comes back a whole
    /// one, so a rule too thin to round to anything still covers a row of pixels.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfRectangle SnapRectToDevicePixels(in PdfRectangle rect, in PdfMatrix deviceMatrix)
    {
        PdfRectangle deviceRect = deviceMatrix.MapRect(rect);
        PdfRectangle snappedDeviceRect = SnapToDevicePixels(deviceRect);

        return deviceMatrix.Invert().MapRect(snappedDeviceRect);
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
    // TODO: a quarter turn maps the axes onto each other instead of leaving them in place, and is
    // rejected here. Rects and image tiles on a page rotated by 90 or 270 degrees are therefore neither
    // snapped nor drawn aliased, while an upright page has them both. Move those callers to
    // IsGridPreserving, which accepts a quarter turn.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAxisAligned(in PdfMatrix matrix)
        => MathF.Abs(matrix.SkewX) <= AxisAlignEpsilon && MathF.Abs(matrix.SkewY) <= AxisAlignEpsilon;

    /// <summary>
    /// Returns whether <paramref name="matrix"/> maps the two axes onto axes, within
    /// <see cref="AxisAlignEpsilon"/>: either it leaves them where they are, or it turns them onto each
    /// other, as a quarter turn does. Geometry that runs along an axis still does after such a matrix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGridPreserving(in PdfMatrix matrix)
    {
        return IsAxisAligned(matrix)
            || (MathF.Abs(matrix.ScaleX) <= AxisAlignEpsilon && MathF.Abs(matrix.ScaleY) <= AxisAlignEpsilon);
    }

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

    /// <summary>
    /// Returns whether the pattern is better covered by repeating a recorded tile than by drawing the
    /// cell once per grid position. A step that does not advance leaves no tile to repeat, and a tile
    /// wider than a decoding tile is not worth rasterizing.
    /// </summary>
    public static bool CanTileByRepeating(DrawTilingCommand command, PdfCommandExecutionContext executionContext)
    {
        if (command.XStep <= 0 || command.YStep <= 0)
        {
            return false;
        }

        PdfMatrix deviceMatrix = GetScaledMatrix(executionContext);
        PdfPoint deviceOrigin = deviceMatrix.MapPoint(PdfPoint.Empty);
        PdfPoint mappedX = deviceMatrix.MapPoint(new PdfPoint(command.XStep, 0));
        PdfPoint mappedY = deviceMatrix.MapPoint(new PdfPoint(0, command.YStep));

        float deviceXAxisX = mappedX.X - deviceOrigin.X;
        float deviceXAxisY = mappedX.Y - deviceOrigin.Y;
        float deviceYAxisX = mappedY.X - deviceOrigin.X;
        float deviceYAxisY = mappedY.Y - deviceOrigin.Y;

        float deviceXAxisLength = MathF.Sqrt((deviceXAxisX * deviceXAxisX) + (deviceXAxisY * deviceXAxisY));
        float deviceYAxisLength = MathF.Sqrt((deviceYAxisX * deviceYAxisX) + (deviceYAxisY * deviceYAxisY));

        int maxTileDeviceDimension = executionContext.Parameters.ImageTileSize;

        return deviceXAxisLength <= maxTileDeviceDimension && deviceYAxisLength <= maxTileDeviceDimension;
    }

    /// <summary>
    /// Returns the cached entry built for the shading, building and storing one when the cache holds
    /// none or holds one built under different parameters.
    /// </summary>
    public static ShadingCommandCacheEntry GetOrBuildShadingEntry(DrawShadingCommand command, PdfCommandExecutionContext executionContext)
    {
        ShadingCommandCacheKey key = new(command.Context);

        lock (executionContext.ContentLocker)
        {
            if (executionContext.Cache.GetEntry(key) is ShadingCommandCacheEntry existing && existing.ParametersMatches(executionContext.Parameters))
            {
                return existing;
            }

            ShadingCommandCacheEntry entry = command.BuildEntry(executionContext);
            executionContext.Cache.StoreEntry(key, entry);
            return entry;
        }
    }
}
