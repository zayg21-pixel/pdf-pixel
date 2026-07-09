using PdfPixel.Color.Paint;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands.Image;

internal static class PdfImageCommandUtilities
{
    /// <summary>
    /// Creates a color matrix that maps a grayscale mask to a solid fill color with
    /// the gray channel used as alpha. Input is Gray8 where R=G=B=gray, A=1.
    /// </summary>
    public static float[] CreateStencilMaskColorMatrix(ref readonly SKColor fillColor, bool inverse)
    {
        float fillR = fillColor.Red / 255f;
        float fillG = fillColor.Green / 255f;
        float fillB = fillColor.Blue / 255f;

        if (inverse)
        {
            return new float[] { -fillR, 0, 0, 0, fillR, 0, -fillG, 0, 0, fillG, 0, 0, -fillB, 0, fillB, -1, 0, 0, 0, 1 };
        }

        return new float[] { fillR, 0, 0, 0, 0, 0, fillG, 0, 0, 0, 0, 0, fillB, 0, 0, 1, 0, 0, 0, 0 };
    }

    /// <summary>
    /// Computes where <paramref name="tilePosition"/> should be drawn, snapping to whole device
    /// pixels when the CTM is axis-aligned, falling back to the tile's native pixel
    /// size with no snapping otherwise.
    /// </summary>
    public static SnappedTilePlacement GetSnappedTilePlacement(
        PdfCommandExecutionContext executionContext, SKSizeI imageSize, SKRectI tilePosition, bool interpolate)
    {
        SKMatrix baseCtm = CommandHelpers.GetScaledMatrix(executionContext);
        SKMatrix ctm = GetImageCtm(baseCtm);
        SKSamplingOptions sampling = GetSamplingOptions(ctm, imageSize, interpolate);

        if (!executionContext.Parameters.SnapToDevicePixels || !CommandHelpers.IsAxisAligned(ctm))
        {
            return GetUnsnappedTilePlacement(imageSize, tilePosition, sampling);
        }

        SKMatrix pixelToDeviceMatrix = ctm.PreConcat(SKMatrix.CreateScale(1f / imageSize.Width, 1f / imageSize.Height));

        SKPoint topLeft = pixelToDeviceMatrix.MapPoint(new SKPoint(tilePosition.Left, tilePosition.Top));
        SKPoint bottomRight = pixelToDeviceMatrix.MapPoint(new SKPoint(tilePosition.Right, tilePosition.Bottom));

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

        SKSize deviceSize = new(deviceWidth, deviceHeight);
        SKMatrix placementMatrix = GetSignedPlacementMatrix(baseCtm, pixelToDeviceMatrix, roundedLeft, roundedTop);

        return new SnappedTilePlacement(deviceSize, placementMatrix, sampling);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SnappedTilePlacement GetUnsnappedTilePlacement(SKSizeI imageSize, SKRectI tilePosition, in SKSamplingOptions sampling)
    {
        SKSizeI fallbackDeviceSize = new(tilePosition.Width, tilePosition.Height);
        SKMatrix fallbackPlacementMatrix = SKMatrix.Concat(
            GetImageMatrix(),
            SKMatrix.Concat(
                SKMatrix.CreateScale(1f / imageSize.Width, 1f / imageSize.Height),
                SKMatrix.CreateTranslation(tilePosition.Left, tilePosition.Top)));

        return new SnappedTilePlacement(fallbackDeviceSize, fallbackPlacementMatrix, sampling);
    }

    /// <summary>
    /// Builds the matrix that places a tile at the rounded device position while re-applying the
    /// axis signs that rounding stripped out, so mirrored images (e.g. the CTM's baked-in Y-flip)
    /// still draw the right way up. <paramref name="canvasCtm"/> is the transform actually on the
    /// canvas at draw time (no image flip), used to cancel it out; <paramref name="pixelToDeviceMatrix"/>
    /// is the image-inclusive matrix the rounded position/sign were computed from.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKMatrix GetSignedPlacementMatrix(SKMatrix canvasCtm, SKMatrix pixelToDeviceMatrix, float roundedLeft, float roundedTop)
    {
        SKMatrix signedPlacement = new(
            MathF.Sign(pixelToDeviceMatrix.ScaleX),
            0,
            roundedLeft,
            0,
            MathF.Sign(pixelToDeviceMatrix.ScaleY),
            roundedTop,
            0,
            0,
            1);

        return SKMatrix.Concat(canvasCtm.Invert(), signedPlacement);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKSamplingOptions GetSamplingOptions(SKMatrix ctm, SKSizeI imageSize, bool interpolate)
    {
        bool isDownscaled = GetScaledSize(ctm, imageSize).HasValue;

        if (isDownscaled || interpolate)
        {
            return new SKSamplingOptions(SKFilterMode.Linear);
        }
        else
        {
            return new SKSamplingOptions(SKFilterMode.Nearest);
        }
    }

    /// <summary>
    /// Returns a scaled size for the given original size based on the current CTM, rounded up so
    /// callers get a decode/sample size with margin rather than one that ever under-shoots the
    /// true target.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKSizeI? GetScaledSize(SKMatrix ctm, SKSizeI size)
    {
        SKSize? exactSize = GetExactScaledSize(ctm, size);

        if (!exactSize.HasValue)
        {
            return null;
        }

        return new SKSizeI(
            Math.Max(1, (int)Math.Round(exactSize.Value.Width)),
            Math.Max(1, (int)Math.Round(exactSize.Value.Height)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKSize? GetExactScaledSize(SKMatrix ctm, SKSizeI size)
    {
        SKPoint unitMapped = ctm.MapPoint(new SKPoint(1, 1)) - ctm.MapPoint(new SKPoint(0, 0));

        float unitPixelsX = Math.Abs(unitMapped.X);
        float unitPixelsY = Math.Abs(unitMapped.Y);

        float relScaleX = unitPixelsX / size.Width;
        float relScaleY = unitPixelsY / size.Height;

        float maxScale = Math.Max(relScaleX, relScaleY);

        if (maxScale >= 0.5f)
        {
            return default;
        }

        return new SKSize(size.Width * maxScale, size.Height * maxScale);
    }

    /// <summary>
    /// Returns the matrix that maps PDF image space to Skia canvas space.
    /// Equivalent to: <c>canvas.Concat(Scale(1,−1))</c> then <c>canvas.Concat(Translate(0,−1))</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKMatrix GetImageMatrix()
        => SKMatrix.Concat(SKMatrix.CreateScale(1, -1), SKMatrix.CreateTranslation(0, -1));

    /// <summary>
    /// Folds <see cref="GetImageMatrix"/> into <paramref name="ctm"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKMatrix GetImageCtm(SKMatrix ctm) => ctm.PreConcat(GetImageMatrix());

    // TODO: [MEDIUM] shall go to paint factory with other paints
    /// <summary>
    /// Creates a paint that draws <paramref name="shader"/> with the blend mode and fill
    /// alpha captured in <paramref name="context"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint GetBaseImagePaint(SKShader shader, ImageDecodingContext context)
    {
        return new()
        {
            Shader = shader,
            BlendMode = context.BlendMode,
            Color = PdfPaintFactory.ApplyAlpha(SKColors.White, context.FillAlpha)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint GetBaseImagePaint(ImageDecodingContext context)
    {
        return new()
        {
            BlendMode = context.BlendMode,
            Color = PdfPaintFactory.ApplyAlpha(SKColors.White, context.FillAlpha)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKRectI ComputeImageRegionOfInterest(SKSizeI imageSize, PdfCommandExecutionContext executionContext)
    {
        SKRectI fullImageBounds = SKRectI.Create(0, 0, imageSize.Width, imageSize.Height);

        if (!executionContext.PageRegionOfInterest.HasValue)
        {
            return fullImageBounds;
        }

        SKMatrix contentToImagePixels = GetImageCtm(executionContext.Frames.TotalMatrix).Invert().PostConcat(SKMatrix.CreateScale(imageSize.Width, imageSize.Height));
        SKRect mapped = contentToImagePixels.MapRect(executionContext.PageRegionOfInterest.Value);
        SKRectI imageRoi = SKRectI.Round(mapped);
        imageRoi.Intersect(fullImageBounds);
        return imageRoi;
    }
}
