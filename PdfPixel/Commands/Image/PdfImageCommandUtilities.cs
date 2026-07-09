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
        SKMatrix ctm = CommandHelpers.GetScaledMatrix(executionContext);
        SKSamplingOptions sampling = GetSamplingOptions(ctm, imageSize, interpolate);

        if (!IsAxisAligned(ctm))
        {
            SKSizeI fallbackDeviceSize = new(tilePosition.Width, tilePosition.Height);
            SKMatrix fallbackPlacementMatrix = SKMatrix.Concat(
                SKMatrix.CreateScale(1f / imageSize.Width, 1f / imageSize.Height),
                SKMatrix.CreateTranslation(tilePosition.Left, tilePosition.Top));

            return new SnappedTilePlacement(fallbackDeviceSize, fallbackPlacementMatrix, sampling);
        }

        SKMatrix pixelToDeviceMatrix = ctm.PreConcat(SKMatrix.CreateScale(1f / imageSize.Width, 1f / imageSize.Height));

        SKRect devicePosition = pixelToDeviceMatrix.MapRect((SKRect)tilePosition);
        SKRect snappedDevicePosition = CommandHelpers.SnapToDevicePixels(devicePosition);

        SKSizeI deviceSize = new(
            (int)(snappedDevicePosition.Right - snappedDevicePosition.Left),
            (int)(snappedDevicePosition.Bottom - snappedDevicePosition.Top));

        SKMatrix placementMatrix = SKMatrix.Concat(ctm.Invert(), SKMatrix.CreateTranslation(snappedDevicePosition.Left, snappedDevicePosition.Top));

        return new SnappedTilePlacement(deviceSize, placementMatrix, sampling);
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
    /// Returns whether <paramref name="ctm"/> is a plain scale (any sign, i.e. flips are fine)
    /// and translation, with no rotation or skew — the only shape of transform for which
    /// snapping a rect to whole device pixels is well-defined.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAxisAligned(SKMatrix ctm)
        => ctm.SkewX == 0 && ctm.SkewY == 0 && ctm.ScaleX != 0 && ctm.ScaleY != 0;

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

        if (maxScale >= 1f)
        {
            return default;
        }

        return new SKSize(size.Width * maxScale, size.Height * maxScale);
    }

    /// <summary>
    /// Returns the matrix that maps PDF image space to Skia canvas space.
    /// Equivalent to: <c>canvas.Concat(Scale(1,−1))</c> then <c>canvas.Concat(Translate(0,−1))</c>.
    /// Emit via <see cref="ConcatMatrixCommand"/> instead of calling canvas directly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKMatrix GetImageMatrix()
        => SKMatrix.Concat(SKMatrix.CreateScale(1, -1), SKMatrix.CreateTranslation(0, -1));

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

    /// <summary>
    /// Maps the current viewport's page-space region of interest into unit-square content space
    /// via CTM, intersected with the unit square and snapped to cover at least one device pixel.
    /// Returns the full unit square when no region of interest is set (the full page is visible).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKRect ComputeContentRegionOfInterest(PdfCommandExecutionContext executionContext)
    {
        SKRect unitSquare = SKRect.Create(0, 0, 1, 1);

        if (!executionContext.PageRegionOfInterest.HasValue)
        {
            return unitSquare;
        }

        SKRect mapped = executionContext.Frames.TotalMatrix.Invert().MapRect(executionContext.PageRegionOfInterest.Value);
        mapped.Intersect(unitSquare);

        SKMatrix ctm = CommandHelpers.GetScaledMatrix(executionContext);
        SKRect deviceRect = ctm.MapRect(mapped);
        SKRect snappedDeviceRect = CommandHelpers.SnapToDevicePixels(deviceRect);

        return ctm.Invert().MapRect(snappedDeviceRect);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKRectI ComputeImageRegionOfInterest(SKSizeI imageSize, PdfCommandExecutionContext executionContext)
    {
        SKRectI fullImageBounds = SKRectI.Create(0, 0, imageSize.Width, imageSize.Height);

        if (!executionContext.PageRegionOfInterest.HasValue)
        {
            return fullImageBounds;
        }

        SKMatrix contentToImagePixels = executionContext.Frames.TotalMatrix.Invert().PostConcat(SKMatrix.CreateScale(imageSize.Width, imageSize.Height));
        SKRect mapped = contentToImagePixels.MapRect(executionContext.PageRegionOfInterest.Value);
        SKRectI imageRoi = SKRectI.Round(mapped);
        imageRoi.Intersect(fullImageBounds);
        return imageRoi;
    }
}
