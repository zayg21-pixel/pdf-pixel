using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands.Processing;

/// <summary>
/// Helper methods for command processing, e.g. for device matrices, pixel snapping, and tiling.
/// </summary>
public static class PdfCommandProcessingUtilities
{
    private const float AxisAlignEpsilon = 0.01f;

    /// <summary>
    /// Returns the command-derived total matrix scaled to <see cref="PdfPixel.Models.PdfCommandExecutionParameters.ScaleFactor"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfMatrix GetScaledMatrix(PdfCommandExecutionContext executionContext)
    {
        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        PdfMatrix totalMatrix = executionContext.Frames.TotalMatrix;

        if (executionContext.Parameters.ScaleFactor.HasValue)
        {
            float scaleValue = executionContext.Parameters.ScaleFactor.Value;
            return totalMatrix.PostConcat(PdfMatrix.CreateScale(scaleValue, scaleValue));
        }

        return totalMatrix;
    }

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
    /// Puts every edge of <paramref name="deviceRect"/> on the whole device pixel nearest to it, and
    /// gives an axis whose edges rounded onto the same boundary a whole pixel of its own, so that a mark
    /// too thin to round to anything still covers a row. The rescued axis keeps the edge it rounded to
    /// and grows away from it, so the edge geometry abutting this one shares stays shared.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfRectangle SnapToDevicePixels(in PdfRectangle deviceRect)
    {
        float left = SnapToWholePixel(deviceRect.Left);
        float top = SnapToWholePixel(deviceRect.Top);

        return new PdfRectangle(
            left,
            top,
            MathF.Max(SnapToWholePixel(deviceRect.Right), left + 1f),
            MathF.Max(SnapToWholePixel(deviceRect.Bottom), top + 1f));
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
    public static float SnapToWholePixel(float deviceCoordinate) => MathF.Floor(deviceCoordinate + 0.5f);

    /// <summary>
    /// Returns how far the pattern's steps advance on the device, measured along each of the pattern's
    /// own axes.
    /// </summary>
    public static PdfSize GetDeviceStepSize(DrawTilingCommand command, PdfCommandExecutionContext executionContext)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        PdfMatrix deviceMatrix = GetScaledMatrix(executionContext);
        PdfPoint deviceOrigin = deviceMatrix.MapPoint(PdfPoint.Empty);
        PdfPoint mappedX = deviceMatrix.MapPoint(new PdfPoint(command.XStep, 0));
        PdfPoint mappedY = deviceMatrix.MapPoint(new PdfPoint(0, command.YStep));

        float deviceXAxisX = mappedX.X - deviceOrigin.X;
        float deviceXAxisY = mappedX.Y - deviceOrigin.Y;
        float deviceYAxisX = mappedY.X - deviceOrigin.X;
        float deviceYAxisY = mappedY.Y - deviceOrigin.Y;

        return new PdfSize(
            MathF.Sqrt((deviceXAxisX * deviceXAxisX) + (deviceXAxisY * deviceXAxisY)),
            MathF.Sqrt((deviceYAxisX * deviceYAxisX) + (deviceYAxisY * deviceYAxisY)));
    }

    /// <summary>
    /// Returns whether the pattern is better covered by repeating a recorded tile than by drawing the
    /// cell once per grid position. A step that does not advance, on the page or on the device, leaves
    /// no tile to repeat, and a tile wider than a decoding tile is not worth rasterizing.
    /// </summary>
    public static bool CanTileByRepeating(DrawTilingCommand command, PdfCommandExecutionContext executionContext)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        if (command.XStep <= 0 || command.YStep <= 0)
        {
            return false;
        }

        PdfSize deviceStep = GetDeviceStepSize(command, executionContext);

        if (deviceStep.Width <= 0 || deviceStep.Height <= 0)
        {
            return false;
        }

        int maxTileDeviceDimension = executionContext.Parameters.ImageTileSize;

        return deviceStep.Width <= maxTileDeviceDimension && deviceStep.Height <= maxTileDeviceDimension;
    }
}
