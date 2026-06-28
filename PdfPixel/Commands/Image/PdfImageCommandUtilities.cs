using PdfPixel.Color.Paint;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal static class PdfImageCommandUtilities
{
    /// <summary>
    /// Returns the matrix that maps PDF image space to Skia canvas space.
    /// Equivalent to: <c>canvas.Concat(Scale(1,−1))</c> then <c>canvas.Concat(Translate(0,−1))</c>.
    /// Emit via <see cref="ConcatMatrixCommand"/> instead of calling canvas directly.
    /// </summary>
    public static SKMatrix GetImageMatrix()
        => SKMatrix.Concat(SKMatrix.CreateScale(1, -1), SKMatrix.CreateTranslation(0, -1));

    /// <summary>
    /// Creates a paint that draws <paramref name="shader"/> with the blend mode and fill
    /// alpha captured in <paramref name="context"/>.
    /// </summary>
    public static SKPaint GetBaseImagePaint(SKShader shader, ImageDecodingContext context)
    {
        return new()
        {
            Shader = shader,
            BlendMode = context.BlendMode,
            Color = PdfPaintFactory.ApplyAlpha(SKColors.White, context.FillAlpha)
        };
    }

    public static SKSamplingOptions GetSamplingOptions(SKMatrix ctm, SKSizeI imageSize, bool interpolate)
    {
        bool isDownscaled = GetScaledSize(ctm, imageSize).HasValue;

        if (isDownscaled || interpolate)
        {
            return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        }

        return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
    }

    /// <summary>
    /// Returns a scaled size for the given original size based on the current CTM.
    /// </summary>
    public static SKSizeI? GetScaledSize(SKMatrix ctm, SKSizeI size)
    {
        SKPoint unitMapped = ctm.MapPoint(new SKPoint(1, 1)) - ctm.MapPoint(new SKPoint(0, 0));

        float unitPixelsX = Math.Abs(unitMapped.X);
        float unitPixelsY = Math.Abs(unitMapped.Y);

        float relScaleX = unitPixelsX / size.Width;
        float relScaleY = unitPixelsY / size.Height;

        float maxScale = Math.Max(relScaleX, relScaleY);

        if (maxScale < 1f)
        {
            int newWidth = Math.Max(1, (int)Math.Floor(size.Width * maxScale));
            int newHeight = Math.Max(1, (int)Math.Floor(size.Height * maxScale));
            return new SKSizeI(newWidth, newHeight);
        }

        return default;
    }

    /// <summary>
    /// Computes tile sizes for two co-rendered images (image + mask) so they share an
    /// identical relative tile grid (same number of tile rows and columns). Uses the
    /// larger image as the baseline — a finer grid on the higher-resolution image
    /// minimises sub-pixel misalignment when shaders composite both layers.
    /// </summary>
    public static (PdfTileInfo imageTileInfo, PdfTileInfo maskTileInfo) ComputePairedTileSizes(
        PdfImage pdfImage, PdfImage maskImage, int defaultTileSize)
    {
        // TODO: [HIGH] this might not work as expected, tiles might be mis-aligned
        float scaleX = (float)maskImage.Width / pdfImage.Width;
        float scaleY = (float)maskImage.Height / pdfImage.Height;
        SKSizeI maskTileSize = new(
            Math.Max(1, (int)Math.Round(defaultTileSize * scaleX)),
            Math.Max(1, (int)Math.Round(defaultTileSize * scaleY)));
        return (
            new PdfTileInfo(new SKSizeI(pdfImage.Width, pdfImage.Height), new SKSizeI(defaultTileSize, defaultTileSize)),
            new PdfTileInfo(new SKSizeI(maskImage.Width, maskImage.Height), maskTileSize));
    }

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

    public static SKRectI ComputeImageRegionOfInterest(SKSizeI imageSize, SKMatrix ctm, PdfCommandExecutionContext executionContext)
    {
        SKRectI fullImageBounds = SKRectI.Create(0, 0, imageSize.Width, imageSize.Height);

        if (!executionContext.PageRegionOfInterest.HasValue)
        {
            return fullImageBounds;
        }

        SKMatrix contentToImagePixels = ctm.Invert().PostConcat(SKMatrix.CreateScale(imageSize.Width, imageSize.Height));
        SKRect mapped = contentToImagePixels.MapRect(executionContext.PageRegionOfInterest.Value);
        SKRectI imageRoi = SKRectI.Round(mapped);
        imageRoi.Intersect(fullImageBounds);
        return (imageRoi.IsEmpty) ? fullImageBounds : imageRoi;
    }
}
