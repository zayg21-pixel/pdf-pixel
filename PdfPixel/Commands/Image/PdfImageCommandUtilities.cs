using PdfPixel.Geometry;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands.Image;

internal static class PdfImageCommandUtilities
{
    /// <summary>
    /// Computes where <paramref name="tilePosition"/> should be drawn, snapping to whole device
    /// pixels when the CTM is axis-aligned, falling back to the tile's native pixel
    /// size with no snapping otherwise.
    /// </summary>
    public static SnappedTilePlacement GetSnappedTilePlacement(
        PdfCommandExecutionContext executionContext, in PdfIntegerSize imageSize, in PdfIntegerRectangle tilePosition, bool interpolate)
    {
        PdfMatrix baseCtm = CommandHelpers.GetScaledMatrix(executionContext);
        PdfMatrix ctm = GetImageCtm(baseCtm);
        bool shouldInterpolate = ShouldInterpolate(ctm, imageSize, interpolate);

        // TODO: [MEDIUM] a quarter turn keeps a tile on the pixel grid but is rejected here, so a page
        // rotated by 90 or 270 degrees never snaps its tiles. Accepting one needs GetSignedPlacementMatrix
        // reworked first: it recovers the axis signs from Sign(ScaleX)/Sign(ScaleY), both zero on a
        // quarter turn, which would leave the placement degenerate.
        if (!executionContext.Parameters.SnapToDevicePixels || !CommandHelpers.IsAxisAligned(ctm))
        {
            return GetUnsnappedTilePlacement(imageSize, tilePosition, shouldInterpolate);
        }

        PdfPoint topLeft = ctm.MapPoint(new PdfPoint(tilePosition.Left / (float)imageSize.Width, tilePosition.Top / (float)imageSize.Height));
        PdfPoint bottomRight = ctm.MapPoint(new PdfPoint(tilePosition.Right / (float)imageSize.Width, tilePosition.Bottom / (float)imageSize.Height));

        float roundedLeft = MathF.Round(topLeft.X);
        float roundedTop = MathF.Round(topLeft.Y);
        float roundedRight = MathF.Round(bottomRight.X);
        float roundedBottom = MathF.Round(bottomRight.Y);

        float deviceWidth = MathF.Abs(roundedRight - roundedLeft);
        if (deviceWidth == 0)
        {
            deviceWidth = 1;
        }

        float deviceHeight = MathF.Abs(roundedBottom - roundedTop);
        if (deviceHeight == 0)
        {
            deviceHeight = 1;
        }

        PdfSize deviceSize = new(deviceWidth, deviceHeight);
        PdfMatrix placementMatrix = GetSignedPlacementMatrix(baseCtm, ctm, roundedLeft, roundedTop);

        return new SnappedTilePlacement(deviceSize, placementMatrix, shouldInterpolate);
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
    /// Builds the matrix that places a tile at the rounded device position while re-applying the
    /// axis signs that rounding stripped out, so mirrored images (e.g. the CTM's baked-in Y-flip)
    /// still draw the right way up. <paramref name="canvasCtm"/> is the transform actually on the
    /// canvas at draw time (no image flip), used to cancel it out; <paramref name="imageCtm"/>
    /// is the image-inclusive matrix the rounded position/sign were computed from.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfMatrix GetSignedPlacementMatrix(in PdfMatrix canvasCtm, in PdfMatrix imageCtm, float roundedLeft, float roundedTop)
    {
        PdfMatrix signedPlacement = new(
            MathF.Sign(imageCtm.ScaleX),
            0,
            roundedLeft,
            0,
            MathF.Sign(imageCtm.ScaleY),
            roundedTop);

        return PdfMatrix.Concat(canvasCtm.Invert(), signedPlacement);
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

        if (CommandHelpers.IsAxisAligned(ctm))
        {
            int scaledWidth = Math.Abs((int)Math.Round(edgeX.X) - (int)Math.Round(origin.X));
            int scaledHeight = Math.Abs((int)Math.Round(edgeY.Y) - (int)Math.Round(origin.Y));

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
