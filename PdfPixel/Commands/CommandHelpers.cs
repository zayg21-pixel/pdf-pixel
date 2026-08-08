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
    /// Puts every edge of <paramref name="deviceRect"/> on the whole device pixel nearest to it. The
    /// rectangle can come back empty on an axis it was too thin to reach a pixel on; a caller that draws
    /// a mark rather than clipping to one gives that axis a pixel of its own.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfRectangle SnapToDevicePixels(in PdfRectangle deviceRect)
    {
        return new(
            SnapToWholePixel(deviceRect.Left),
            SnapToWholePixel(deviceRect.Top),
            SnapToWholePixel(deviceRect.Right),
            SnapToWholePixel(deviceRect.Bottom));
    }

    /// <summary>
    /// Rounds one device coordinate to the whole pixel nearest to it, taking a coordinate exactly
    /// halfway to the higher one. Each coordinate is rounded on its own, so every command sends a given
    /// coordinate to the same pixel whatever geometry it belongs to, and two rectangles sharing an edge
    /// come out still sharing it rather than with a gap or an overlap between them. Rounding a halfway
    /// coordinate the same way wherever it sits is what keeps a rectangle a whole number of pixels wide
    /// from changing width as it moves across the grid.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SnapToWholePixel(float deviceCoordinate) => MathF.Floor(deviceCoordinate + 0.5f);

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
