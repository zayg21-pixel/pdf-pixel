using PdfPixel.Commands.Context;
using PdfPixel.Commands.Processing;
using PdfPixel.Geometry;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands.Image;

internal static class PdfImageCommandUtilities
{
    /// <summary>
    /// Computes where <paramref name="tilePosition"/> should be drawn, putting the tile on whole device
    /// pixels under a matrix that keeps the axes on the grid, and falling back to the tile's native pixel
    /// size with no snapping otherwise. The snapped region is brought back into the space the current
    /// transformation is given in, the space paths are drawn and clipped in, so that a tile edge and the
    /// edge of a fill or clip beside it are carried onto the device by the same transform and land on the
    /// same pixel.
    /// </summary>
    public static SnappedTilePlacement GetSnappedTilePlacement(
        PdfCommandExecutionContext executionContext, in PdfIntegerSize imageSize, in PdfIntegerRectangle tilePosition, bool interpolate)
    {
        PdfMatrix deviceMatrix = PdfCommandProcessingUtilities.GetScaledMatrix(executionContext);
        PdfMatrix imageCtm = GetImageCtm(deviceMatrix);
        bool shouldInterpolate = ShouldInterpolate(imageCtm, imageSize, interpolate);

        if (!executionContext.Parameters.SnapToDevicePixels || !PdfCommandProcessingUtilities.IsGridPreserving(deviceMatrix))
        {
            return GetUnsnappedTilePlacement(imageSize, tilePosition, shouldInterpolate);
        }

        PdfRectangle tileImageRect = new(
            tilePosition.Left / (float)imageSize.Width,
            tilePosition.Top / (float)imageSize.Height,
            tilePosition.Right / (float)imageSize.Width,
            tilePosition.Bottom / (float)imageSize.Height);

        PdfRectangle deviceRect = PdfCommandProcessingUtilities.SnapToDevicePixels(imageCtm.MapRect(tileImageRect));
        PdfRectangle sourceRect = deviceMatrix.Invert().MapRect(deviceRect);

        // How many device pixels the tile is sampled to along each of its own axes. A quarter turn
        // carries those axes onto the other one, so the device rectangle measures them the other way
        // round; where the tile ends up on the page is the transformation's business, not this one's.
        bool isQuarterTurn = !PdfCommandProcessingUtilities.IsAxisAligned(deviceMatrix);
        PdfSize deviceSize = new(
            isQuarterTurn ? deviceRect.Height : deviceRect.Width,
            isQuarterTurn ? deviceRect.Width : deviceRect.Height);

        return new SnappedTilePlacement(deviceSize, GetPlacementMatrix(sourceRect, deviceSize), shouldInterpolate);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SnappedTilePlacement GetUnsnappedTilePlacement(in PdfIntegerSize imageSize, in PdfIntegerRectangle tilePosition, bool interpolate)
    {
        PdfSize fallbackDeviceSize = new(tilePosition.Width, tilePosition.Height);
        PdfMatrix fallbackPlacementMatrix = PdfMatrix.Concat(
            GetImageMatrix(),
            PdfMatrix.Concat(
                PdfMatrix.CreateScale(1f / imageSize.Width, 1f / imageSize.Height),
                PdfMatrix.CreateTranslation(tilePosition.Left, tilePosition.Top)));

        return new SnappedTilePlacement(fallbackDeviceSize, fallbackPlacementMatrix, interpolate);
    }

    /// <summary>
    /// Builds the matrix that lays a tile sampled at <paramref name="deviceSize"/> into
    /// <paramref name="sourceRect"/>, the region it covers in the space the current transformation is
    /// given in. The tile's rows run down that space while it runs up, so the matrix turns them over;
    /// how the tile is then turned or mirrored onto the page is left to the transformation itself.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfMatrix GetPlacementMatrix(in PdfRectangle sourceRect, in PdfSize deviceSize)
    {
        return new(
            sourceRect.Width / deviceSize.Width,
            0,
            sourceRect.Left,
            0,
            -sourceRect.Height / deviceSize.Height,
            sourceRect.Bottom);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldInterpolate(in PdfMatrix ctm, in PdfIntegerSize imageSize, bool interpolate)
    {
        bool isDownscaled = GetScaledSize(ctm, imageSize).HasValue;

        return isDownscaled || interpolate;
    }

    /// <summary>
    /// Returns a scaled size for the given original size based on the current CTM, rounded up so
    /// callers get a decode/sample size with margin rather than one that ever under-shoots the
    /// true target.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfIntegerSize? GetScaledSize(in PdfMatrix ctm, in PdfIntegerSize size)
    {
        PdfPoint origin = ctm.MapPoint(PdfPoint.Empty);
        PdfPoint edgeX = ctm.MapPoint(new PdfPoint(1, 0));
        PdfPoint edgeY = ctm.MapPoint(new PdfPoint(0, 1));

        float unitPixelsX = Distance(origin, edgeX);
        float unitPixelsY = Distance(origin, edgeY);

        float relScaleX = unitPixelsX / size.Width;
        float relScaleY = unitPixelsY / size.Height;

        float maxScale = Math.Max(relScaleX, relScaleY);

        if (maxScale >= 1f)
        {
            return default;
        }

        if (PdfCommandProcessingUtilities.IsGridPreserving(ctm))
        {
            // A quarter turn carries the image's own x axis onto the device y axis and its y axis onto
            // the device x axis, so each is measured wherever the matrix put it. Measuring between
            // snapped coordinates is what makes the size decoded here the size the tile is placed at.
            bool isQuarterTurn = !PdfCommandProcessingUtilities.IsAxisAligned(ctm);
            float imageXAxisStart = isQuarterTurn ? origin.Y : origin.X;
            float imageXAxisEnd = isQuarterTurn ? edgeX.Y : edgeX.X;
            float imageYAxisStart = isQuarterTurn ? origin.X : origin.Y;
            float imageYAxisEnd = isQuarterTurn ? edgeY.X : edgeY.Y;

            var scaledWidth = (int)MathF.Abs(PdfCommandProcessingUtilities.SnapToWholePixel(imageXAxisEnd) - PdfCommandProcessingUtilities.SnapToWholePixel(imageXAxisStart));
            var scaledHeight = (int)MathF.Abs(PdfCommandProcessingUtilities.SnapToWholePixel(imageYAxisEnd) - PdfCommandProcessingUtilities.SnapToWholePixel(imageYAxisStart));

            return new PdfIntegerSize(Math.Max(1, scaledWidth), Math.Max(1, scaledHeight));
        }

        PdfSize exactSize = new(size.Width * maxScale, size.Height * maxScale);

        return new PdfIntegerSize(
            Math.Max(1, (int)Math.Round(exactSize.Width)),
            Math.Max(1, (int)Math.Round(exactSize.Height)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Distance(in PdfPoint a, in PdfPoint b)
    {
        float deltaX = b.X - a.X;
        float deltaY = b.Y - a.Y;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// Returns the matrix that maps PDF image space to device space.
    /// Equivalent to concatenating a (1,−1) scale then a (0,−1) translation onto the current transformation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfMatrix GetImageMatrix()
        => PdfMatrix.Concat(PdfMatrix.CreateScale(1, -1), PdfMatrix.CreateTranslation(0, -1));

    /// <summary>
    /// Folds <see cref="GetImageMatrix"/> into <paramref name="ctm"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfMatrix GetImageCtm(in PdfMatrix ctm) => ctm.PreConcat(GetImageMatrix());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfIntegerRectangle ComputeImageRegionOfInterest(in PdfIntegerSize imageSize, PdfCommandExecutionContext executionContext)
    {
        PdfIntegerRectangle fullImageBounds = new(0, 0, imageSize.Width, imageSize.Height);

        if (!executionContext.PageRegionOfInterest.HasValue)
        {
            return fullImageBounds;
        }

        PdfMatrix contentToImagePixels = GetImageCtm(executionContext.Frames.TotalMatrix).Invert().PostConcat(PdfMatrix.CreateScale(imageSize.Width, imageSize.Height));
        PdfRectangle mapped = contentToImagePixels.MapRect(executionContext.PageRegionOfInterest.Value);
        PdfIntegerRectangle roundedRegionOfInterest = new(
            (int)MathF.Round(mapped.Left),
            (int)MathF.Round(mapped.Top),
            (int)MathF.Round(mapped.Right),
            (int)MathF.Round(mapped.Bottom));

        return PdfIntegerRectangle.Intersect(roundedRegionOfInterest, fullImageBounds);
    }
}
