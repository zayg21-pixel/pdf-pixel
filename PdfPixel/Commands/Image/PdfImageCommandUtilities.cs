using PdfPixel.Color.Paint;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Collections.Generic;

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
        return new SKPaint
        {
            Shader = shader,
            BlendMode = context.BlendMode,
            Color = PdfPaintFactory.ApplyAlpha(SKColors.White, context.FillAlpha),
        };
    }

    /// <summary>
    /// Returns the appropriate sampling options for <paramref name="pdfImage"/> given the
    /// current decoding context.  Use <see cref="SKFilterMode.Nearest"/> for stencil masks
    /// regardless — they are always 1-bit and interpolation would corrupt the alpha shape.
    /// </summary>
    public static SKSamplingOptions GetSamplingOptions(SKMatrix ctm, ImageDecodingContext context, PdfImage pdfImage)
        => GetSamplingOptions(ctm, context, new SKSizeI(pdfImage.Width, pdfImage.Height), pdfImage.Interpolate);

    public static SKSamplingOptions GetSamplingOptions(SKMatrix ctm, ImageDecodingContext context, SKSizeI imageSize, bool interpolate)
    {
        bool isDownscaled = GetScaledSize(ctm, imageSize).HasValue;

        if (isDownscaled || context.IsType3Rendering || interpolate)
            return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
    }

    /// <summary>
    /// Returns a scaled size for the given original size based on the current CTM.
    /// </summary>
    public static SKSizeI? GetScaledSize(SKMatrix ctm, SKSizeI size)
    {
        var unitMapped = ctm.MapPoint(new SKPoint(1, 1)) - ctm.MapPoint(new SKPoint(0, 0));

        float unitPixelsX = Math.Abs(unitMapped.X);
        float unitPixelsY = Math.Abs(unitMapped.Y);

        float relScaleX = unitPixelsX / size.Width;
        float relScaleY = unitPixelsY / size.Height;

        float maxScale = Math.Max(relScaleX, relScaleY);

        if (maxScale < 1f)
        {
            var newWidth = Math.Max(1, (int)Math.Floor(size.Width * maxScale));
            var newHeight = Math.Max(1, (int)Math.Floor(size.Height * maxScale));
            return new SKSizeI(newWidth, newHeight);
        }

        return default;
    }

    /// <summary>
    /// Yields the source-pixel rects of a fixed-size tile grid covering the image.
    /// </summary>
    public static IEnumerable<SKRectI> EnumerateTiles(SKSizeI size, int tileSize)
    {
        for (int y = 0; y < size.Height; y += tileSize)
        {
            int h = System.Math.Min(tileSize, size.Height - y);
            for (int x = 0; x < size.Width; x += tileSize)
            {
                int w = System.Math.Min(tileSize, size.Width - x);
                yield return new SKRectI(x, y, x + w, y + h);
            }
        }
    }

    /// <summary>
    /// Returns the mask pixel rect that covers the image tile's proportional region.
    /// Uses floor for the start and ceil for the end so the subset includes every mask pixel
    /// touched by the tile. The fractional remainder is handled by the shader local matrix
    /// in <see cref="ImageBlending.BuildMaskTileShader"/>.
    /// </summary>
    public static SKRectI GetMaskTileRect(SKRectI imageTile, SKSizeI imageSize, SKSizeI maskSize)
    {
        float scaleX = (float)maskSize.Width / imageSize.Width;
        float scaleY = (float)maskSize.Height / imageSize.Height;

        int left = (int)Math.Floor(imageTile.Left * scaleX);
        int top = (int)Math.Floor(imageTile.Top * scaleY);
        int right = Math.Min(maskSize.Width, (int)Math.Ceiling(imageTile.Right * scaleX));
        int bottom = Math.Min(maskSize.Height, (int)Math.Ceiling(imageTile.Bottom * scaleY));

        return new SKRectI(left, top, right, bottom);
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
        float scaleX = (float)maskImage.Width / pdfImage.Width;
        float scaleY = (float)maskImage.Height / pdfImage.Height;
        SKSizeI maskTileSize = new SKSizeI(
            Math.Max(1, (int)Math.Round(defaultTileSize * scaleX)),
            Math.Max(1, (int)Math.Round(defaultTileSize * scaleY)));
        return (
            new PdfTileInfo(new SKSizeI(pdfImage.Width, pdfImage.Height), new SKSizeI(defaultTileSize, defaultTileSize)),
            new PdfTileInfo(new SKSizeI(maskImage.Width, maskImage.Height), maskTileSize));
    }

    /// <summary>
    /// Computes the image-space region of interest for a given image, CTM, and execution context.
    /// Maps the page-space ROI through the inverse CTM into image pixel coordinates,
    /// then clamps to the full image bounds. Returns the full image bounds when no ROI is set.
    /// </summary>
    public static SKRectI ComputeImageRegionOfInterest(PdfImage image, SKMatrix ctm, PdfCommandExecutionContext executionContext)
        => ComputeImageRegionOfInterest(new SKSizeI(image.Width, image.Height), ctm, executionContext);

    public static SKRectI ComputeImageRegionOfInterest(SKSizeI imageSize, SKMatrix ctm, PdfCommandExecutionContext executionContext)
    {
        var fullImageBounds = SKRectI.Create(0, 0, imageSize.Width, imageSize.Height);

        if (!executionContext.PageRegionOfInterest.HasValue)
            return fullImageBounds;

        SKRect mapped = ctm.Invert().MapRect(executionContext.PageRegionOfInterest.Value);
        SKRectI imageRoi = SKRectI.Round(mapped);
        imageRoi.Intersect(fullImageBounds);
        return imageRoi.IsEmpty ? fullImageBounds : imageRoi;
    }

    // TODO: add method similar to SKPath drawing to check if AA is allowed
}
